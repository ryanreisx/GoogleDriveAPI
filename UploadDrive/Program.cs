using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace UploadDrive
{
    class Program
    {
        private const string PathToServiceAccountKeyFile = @"C:\Users\Ryan\Desktop\cred\ServiceAccountCred.json";
        private const string ServiceAccountEmail = @"youservice";
        private const string UploadFileName = "cred";
        private const string DirectoryId = @"yourDirectory";
        private const string LocalFolderPath = @"yourLocal";
        private const string folderId = @"yourId";
        private const string folderName = "yourfolderName";


        static async Task Main(string[] args)
        {

            //await UploadFolderToGoogleDriveAsync(UploadFileName, DirectoryId);
            var credential = GoogleCredential.FromFile(PathToServiceAccountKeyFile).CreateScoped(DriveService.ScopeConstants.Drive);
            using var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential
            });
            await DownloadFolderFromGoogleDrive(service, folderName, LocalFolderPath);
        }

        static async Task UploadFolderToGoogleDriveAsync(string folderPath, string parentDirectoryId)
        {
            var credential = GoogleCredential.FromFile(PathToServiceAccountKeyFile).CreateScoped(DriveService.ScopeConstants.Drive);

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            // Criar pasta no Google Drive
            var driveFolder = new Google.Apis.Drive.v3.Data.File()
            {
                Name = "crod",
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string>() { parentDirectoryId }
            };
            driveFolder = await service.Files.Create(driveFolder).ExecuteAsync();

            // Fazer o upload de cada arquivo na pasta local para a pasta no Google Drive
            await UploadFilesInFolderAsync(service, driveFolder.Id, folderPath);
        }

        static async Task UploadFilesInFolderAsync(DriveService service, string driveFolderId, string folderPath)
        {
            // Fazer o upload de cada arquivo na pasta local para a pasta no Google Drive
            foreach (string filePath in Directory.GetFiles(folderPath))
            {
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = Path.GetFileName(filePath),
                    Parents = new List<string>() { driveFolderId }
                };

                string uploadedFileId;

                await using (var fsSource = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var request = service.Files.Create(fileMetadata, fsSource, "text/plain");
                    request.Fields = "*";
                    var results = await request.UploadAsync(CancellationToken.None);

                    if (results.Status == UploadStatus.Failed)
                    {
                        Console.WriteLine($"Error Upload: {results.Exception.Message}");
                    }
                    uploadedFileId = request.ResponseBody?.Id;
                }
            }

            // Processar cada subdiretório na pasta local
            foreach (string subFolderPath in Directory.GetDirectories(folderPath))
            {
                // Criar subpasta no Google Drive
                var driveSubFolder = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = Path.GetFileName(subFolderPath),
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string>() { driveFolderId }
                };
                driveSubFolder = await service.Files.Create(driveSubFolder).ExecuteAsync();

                // Fazer o upload de cada arquivo na subpasta local para a subpasta no Google Drive
                await UploadFilesInFolderAsync(service, driveSubFolder.Id, subFolderPath);
            }
        }

        static async Task DownloadFolderFromGoogleDrive(DriveService service, string folderName, string localPath)
        {
            // Cria o diretório local para a pasta
            Directory.CreateDirectory(localPath);

            // Procura a pasta pelo nome
            var request = service.Files.List();
            request.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed = false";
            var response = await request.ExecuteAsync();

            if (response.Files.Count > 0)
            {
                // Se a pasta for encontrada, baixa seu conteúdo
                var folder = response.Files[0];
                await DownloadFolderContents(service, folder.Id, localPath);
            }
            else
            {
                Console.WriteLine($"Pasta com o nome '{folderName}' não encontrada.");
            }
        }

        static async Task DownloadFolderContents(DriveService service, string folderId, string localPath)
        {
            // Lista todos os arquivos dentro da pasta
            var request = service.Files.List();
            request.Q = $"parents in '{folderId}'";
            var response = await request.ExecuteAsync();

            foreach (var driveFile in response.Files)
            {
                if (driveFile.MimeType == "application/vnd.google-apps.folder")
                {
                    // Se for uma subpasta, chama recursivamente este método para baixar seu conteúdo
                    var subfolderPath = Path.Combine(localPath, driveFile.Name);
                    await DownloadFolderContents(service, driveFile.Id, subfolderPath);
                }
                else
                {
                    // Se for um arquivo, baixa para o diretório local
                    var getRequest = service.Files.Get(driveFile.Id);
                    var destinationPath = Path.Combine(localPath, driveFile.Name);
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                    await getRequest.DownloadAsync(fileStream);
                    Console.WriteLine($"Downloaded file: {driveFile.Name} to {destinationPath}");
                }
            }
        }







    }
}


