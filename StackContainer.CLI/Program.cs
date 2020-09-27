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
            StackContainer settings = GetSettings();

            settings.OpenContainer("products");

            foreach (string valueName in settings.GetValueNames())
            {
                Console.WriteLine(settings.ReadValue(valueName, Encoding.ASCII));
            }

            settings.Back();

            settings.OpenContainer("config");

            bool show_help = BitConverter.ToBoolean(settings.ReadValue("show_help"), 0);
            int id = BitConverter.ToInt32(settings.ReadValue("store_id"), 0);
            
            settings.Back();

            if (show_help)
            {
                Console.WriteLine("This is help!");
            }

            Console.WriteLine("Store id : {0}", id);

            if (Console.ReadLine().ToLowerInvariant().StartsWith("y"))
            {
                settings.OpenContainer("config");

                if (settings.ValueExists("value"))
                {
                    Console.WriteLine(settings.ReadValue("value", Encoding.UTF8));
                }

                settings.WriteValue("value", Console.ReadLine(), Encoding.UTF8);
                settings.Back();
                
                File.WriteAllBytes("settings.bin", settings.Serialize());
            }


            string fileName = "container.bin";
            string directoryPath = Environment.CurrentDirectory;

            StackContainer container;

            if (File.Exists(fileName))
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

        private static StackContainer GetSettings()
        {
            if (!File.Exists("settings.bin"))
            {
                StackContainer settings = new StackContainer();

                settings.CreateContainer("products");

                settings.OpenContainer("products");
                settings.WriteValue("product1", "Product 1 Example Name", Encoding.ASCII);
                settings.WriteValue("product2", "Product 2 Example Name", Encoding.ASCII);
                settings.WriteValue("product3", "Product 3 Example Name", Encoding.ASCII);
                settings.Back();

                settings.CreateContainer("config");

                settings.OpenContainer("config");
                settings.WriteValue("show_help", BitConverter.GetBytes(false), true);
                settings.WriteValue("store_id", BitConverter.GetBytes(125778), true);

                settings.Back();
                File.WriteAllBytes("setting.bin", settings.Serialize());
                return settings;
            }
            else
            {
                return new StackContainer(File.ReadAllBytes("settings.bin"));
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
