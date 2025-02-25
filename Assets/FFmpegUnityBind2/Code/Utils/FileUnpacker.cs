using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;
using UnityEngine.Networking;

namespace FFmpegUnityBind2.Utils
{
    public class FileUnpacker : MonoBehaviour
    {
        static FileUnpacker instance;
        static FileUnpacker Instance
        {
            get
            {
                if(!instance)
                {
                    instance = new GameObject(nameof(FileUnpacker)).AddComponent<FileUnpacker>();
                }
                return instance;
            }
        }

        public static void UnpackFile(string relativePath, string destinationPath)
        {
            Instance.StartCoroutine(UnpackFileOperation(relativePath, destinationPath));
        }

        public static void UnpackFiles(string[] relativePaths, string[] destinationPaths)
        {
            Instance.StartCoroutine(UnpackFilesOperation(relativePaths, destinationPaths));
        }

        public static IEnumerator UnpackFileOperation(string relativePath, string mainDestinationPath)
        {
            Debug.Log($"relativePath: {relativePath}");
            Debug.Log($"destinationPath: {mainDestinationPath}");

            // Decide if the file is expected to be zipped or not.
            bool isZipped = !relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

            // Only change the extension if the file is zipped.
            string streamingAssetsFilePath = isZipped ? ToZipPath(relativePath) : relativePath;
            string streamingAssetsSourcePath = Path.Combine(Application.streamingAssetsPath, streamingAssetsFilePath);
            Debug.Log($"streamingAssetsSourcePath: {streamingAssetsSourcePath}");

            // For Android, streamingAssetsPath returns a URI inside the APK.
            string streamingAssetsSourceUri = Application.platform == RuntimePlatform.Android
                ? streamingAssetsSourcePath
                : "file://" + streamingAssetsSourcePath;
            Debug.Log($"streamingAssetsSourceUri: {streamingAssetsSourceUri}");

            // Use UnityWebRequest instead of WWW (WWW is obsolete).
            UnityWebRequest request = UnityWebRequest.Get(streamingAssetsSourceUri);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error downloading file: {request.error}");
                yield break;
            }

            byte[] bytes = request.downloadHandler.data;
            if (bytes == null)
            {
                Debug.LogError("Downloaded bytes are null!");
                yield break;
            }

            string destinationDirectory = Path.GetDirectoryName(mainDestinationPath);
            Directory.CreateDirectory(destinationDirectory);
            Debug.Log($"destinationDirectory: {destinationDirectory}");

            if (isZipped)
            {
                // Save the zip file.
                string destinationZipPath = ToZipPath(mainDestinationPath);
                File.WriteAllBytes(destinationZipPath, bytes);
                Debug.Log($"destinationZipPath: {destinationZipPath}");

                // Extract the zip file.
                string tempDestinationDirectory = Path.Combine(destinationDirectory, "TempUnzipDirectory");
                Directory.CreateDirectory(tempDestinationDirectory);
                Debug.Log($"tempDestinationDirectory: {tempDestinationDirectory}");

                new FastZip().ExtractZip(destinationZipPath, tempDestinationDirectory, null);

                // Assume the extracted file has the same name as the intended destination.
                string extractedFilePath = Path.Combine(tempDestinationDirectory, Path.GetFileName(mainDestinationPath));
                if (File.Exists(extractedFilePath))
                {
                    File.Move(extractedFilePath, mainDestinationPath);
                }
                else
                {
                    Debug.LogError("Extracted file not found!");
                }

                // Clean up.
                File.Delete(destinationZipPath);
                Directory.Delete(tempDestinationDirectory, true);
            }
            else
            {
                // Directly write the non-zipped file.
                File.WriteAllBytes(mainDestinationPath, bytes);
                Debug.Log($"File copied to: {mainDestinationPath}");
            }
        }

        static IEnumerator UnpackFilesOperation(string[] relativePaths, string[] destinationPaths)
        {
            if(relativePaths.Length != destinationPaths.Length)
            {
                throw new ArgumentException("relativePaths.Length should be equal destinationPaths.Length");
            }

            for(int i = 0; i < relativePaths.Length; ++i)
            {
                yield return UnpackFileOperation(relativePaths[i], destinationPaths[i]);
            }
        }

        static string ToZipPath(string path)
        {
            return Path.ChangeExtension(path, ".zip");
        }
    }
}