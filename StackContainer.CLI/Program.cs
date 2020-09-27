using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace StackContainer.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            string fileName = "container.bin";
            string directoryPath = Environment.CurrentDirectory;

            StackContainer container;

            if(File.Exists(fileName))
            {
                Console.WriteLine("StackContainer To Folder");

                container = new StackContainer(File.ReadAllBytes(fileName));
                ExportToPath(container, Path.Combine(directoryPath, Path.GetFileName(directoryPath)));

                File.Delete(fileName);
            }
            else
            {

                Console.WriteLine("Folder to StackContainer");

                container = new StackContainer();
                ImportFromPath(container, directoryPath);

                byte[] containerData = container.Serialize();

                File.WriteAllBytes(fileName, containerData);
            }
            
            

        }

        private static void ExportToPath(StackContainer container, string path)
        {

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path).Refresh();
            }

            foreach (string value in container.GetValueNames())
            {
                File.WriteAllBytes(Path.Combine(path, value), container.ReadValue(value));
            }

            foreach (string sub in container.GetContainerNames())
            {
                if (sub == "..")
                    continue;

                string subpath = Path.Combine(path, sub);
                container.OpenContainer(sub, false);
                ExportToPath(container, subpath);
                container.Back();
            }
        }

        private static void ImportFromPath(StackContainer container, string path)
        {
            if (File.Exists(path))
            {
                container.WriteValue(Path.GetFileName(path), File.ReadAllBytes(path));
                return;
            }

            if (Directory.Exists(path))
            {
                foreach (string fileName in Directory.GetFiles(path))
                {
                    ImportFromPath(container, fileName);
                }

                foreach (string folderName in Directory.GetDirectories(path))
                {
                    container.OpenContainer(Path.GetFileName(folderName), true);
                    ImportFromPath(container, folderName);
                    container.Back();
                }
            }
        }

    }
}
