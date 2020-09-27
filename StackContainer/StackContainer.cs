using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StackContainer
{  
    public class StackContainer
    {
        private Stack<Dictionary<string, object>> containerTree;

        private Dictionary<string, object> currentContainer { get { return containerTree.Peek(); } }

        private StackContainer(Dictionary<string, object> start)
        {
            containerTree = new Stack<Dictionary<string, object>>();

            containerTree.Push(start);
        }

        public StackContainer(byte[] container) : this(Deserialize(container))
        {

        }

        public StackContainer() : this(new Dictionary<string, object>())
        {
            currentContainer.Add("..", currentContainer);
        }

        public void Join(params StackContainer[] sources)
        {
            foreach (StackContainer source in sources)
                Join(source);
        }

        public void Join(StackContainer source, bool overwrite = false)
        {
            Join(this, source, "", overwrite);
        }

        public bool TryOpenContainer(string name)
        {
            try
            {
                OpenContainer(name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ContainerExists(string name)
        {
            return currentContainer.ContainsKey(name) && currentContainer[name] is Dictionary<string, object>;
        }

        public void OpenContainer(string name, bool createNew = false)
        {
            if (name == "..")
            {
                if (!Back())
                    throw new StackContainerException();
            }

            if (!ContainerExists(name))
            {
                if (!createNew)
                    throw new StackContainerException("'{0}' not found", name);

                CreateContainer(name);
            }

            Dictionary<string, object> sub = currentContainer[name] as Dictionary<string, object>;

            if (sub == null)
            {
                throw new StackContainerException("'{0}' not a container", name);
            }

            containerTree.Push(sub);
        }

        public bool Back()
        {
            if (containerTree.Count > 1)
            {
                containerTree.Pop();
                return true;
            }
            return false;
        }

        public void BackToRoot()
        {
            while (Back()) ;

        }

        public string[] GetContainerNames()
        {
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, object> k in currentContainer)
            {
                if (k.Value is Dictionary<string, object>)
                    names.Add(k.Key);
            }
            return names.ToArray();
        }

        public string[] GetValueNames()
        {
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, object> k in currentContainer)
            {
                if (!(k.Value is Dictionary<string, object>))
                    names.Add(k.Key);
            }
            return names.ToArray();
        }

        public void CreateContainer(string name)
        {
            if (ContainerExists(name))
            {
                throw new StackContainerException("'{0}' exists", name);
            }

            Dictionary<string, object> sub = new Dictionary<string, object>();
            currentContainer.Add(name, sub);
        }

        public bool ValueExists(string name)
        {
            return currentContainer.ContainsKey(name) && currentContainer[name] is byte[];
        }

        public byte[] ReadValue(string name)
        {
            if (!ValueExists(name))
                throw new StackContainerException("'{0}' not found", name);

            return currentContainer[name] as byte[];
        }

        public string ReadValue(string name, Encoding encoding)
        {
            return encoding.GetString(ReadValue(name));
        }

        public void WriteValue(string name, byte[] value, bool overwrite = true)
        {
            if (!ValueExists(name))
                currentContainer.Add(name, null);
            else if (!overwrite)
                throw new StackContainerException("'{0}' exists");

            currentContainer[name] = value;
        }

        public void WriteValue(string name, string text, Encoding encoding)
        {
            if (string.IsNullOrEmpty(text))
                text = "";

            WriteValue(name, encoding.GetBytes(text));
        }

        public void Delete(string name)
        {
            if (currentContainer.ContainsKey(name))
                currentContainer.Remove(name);
            else
                throw new StackContainerException("'{0}' not found");
        }

        public byte[] Serialize()
        {
            BackToRoot();

            return Serialize(currentContainer);
        }

        private static void Join(StackContainer destination, StackContainer source, string name, bool overwrite)
        {
            if (name == "..")
                return;

            if (!string.IsNullOrEmpty(name))
            {
                source.OpenContainer(name);
                if (!destination.TryOpenContainer(name))
                {
                    destination.CreateContainer(name);
                    destination.OpenContainer(name);
                }
            }

            foreach (string value in source.GetValueNames())
            {
                if (!overwrite && destination.ValueExists(value))
                {
                    continue;
                }

                destination.WriteValue(value, source.ReadValue(value));
            }

            foreach (string sub in source.GetContainerNames())
            {
                Join(destination, source, sub, overwrite);
            }

            if (!string.IsNullOrEmpty(name) && (!source.Back() || !destination.Back()))
            {
                throw new StackContainerException();
            }
        }

        private static byte[] Serialize(Dictionary<string, object> container)
        {
            List<KeyValuePair<string, object>> entities = new List<KeyValuePair<string, object>>();

            Read(container, ref entities);

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms, Encoding.Unicode))
                {
                    bw.Write(entities.Count);

                    for (int i = 0; i < entities.Count; i++)
                    {
                        bw.Write(entities[i].Key);

                        bool isFile = !(entities[i].Value is Dictionary<string, object>);
                        bw.Write(isFile);

                        if (isFile)
                        {
                            byte[] fileData = entities[i].Value as byte[];
                            bw.Write(fileData.Length);
                            bw.Write(fileData);
                        }
                        else
                        {
                            Dictionary<string, object> subContainer = entities[i].Value as Dictionary<string, object>;
                            bw.Write(subContainer.Count);
                            foreach (KeyValuePair<string, object> subIem in subContainer)
                            {
                                int index = entities.IndexOf(subIem);
                                bw.Write(index);
                            }
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        private static Dictionary<string, object> Deserialize(byte[] container)
        {
            List<KeyValuePair<string, object>> entities = new List<KeyValuePair<string, object>>();

            using (MemoryStream ms = new MemoryStream(container))
            {
                using (BinaryReader br = new BinaryReader(ms, Encoding.Unicode))
                {
                    int entitiesCount = br.ReadInt32();
                    for (int i = 0; i < entitiesCount; i++)
                    {
                        string name = br.ReadString();
                        bool file = br.ReadBoolean();
                        if (file)
                        {
                            entities.Add(new KeyValuePair<string, object>(name, br.ReadBytes(br.ReadInt32())));
                        }
                        else
                        {
                            List<int> subItems = new List<int>();
                            int subItemsCount = br.ReadInt32();
                            for (int j = 0; j < subItemsCount; j++)
                            {
                                subItems.Add(br.ReadInt32());
                            }
                            entities.Add(new KeyValuePair<string, object>(name, subItems.ToArray()));
                        }
                    }
                }
            }

            Dictionary<string, object> containerDictionary = new Dictionary<string, object>();

            Write(containerDictionary, entities.ToArray(), 0, 0);

            return containerDictionary;
        }

        private static void Write(Dictionary<string, object> dictionary, KeyValuePair<string, object>[] entities, int parentItem, int currentItem)
        {
            KeyValuePair<string, object> container = entities[currentItem];

            int[] subItems = container.Value as int[];

            if (subItems == null)
                return;

            foreach (int subItem in subItems)
            {

                if (subItem == parentItem)
                {
                    continue;
                }

                KeyValuePair<string, object> sub = entities[subItem];

                if (subItem == -1)
                {
                    throw new StackContainerException();
                }

                if (!(sub.Value is byte[]))
                {
                    Dictionary<string, object> subDictionary = new Dictionary<string, object>();

                    Write(subDictionary, entities, currentItem, subItem);

                    dictionary.Add(sub.Key, subDictionary);
                }
                else
                {
                    dictionary.Add(sub.Key, sub.Value);
                }
            }
        }

        private static void Read(Dictionary<string, object> container, ref List<KeyValuePair<string, object>> entities)
        {
            foreach (KeyValuePair<string, object> subItem in container)
            {
                if (subItem.Value is Dictionary<string, object>)
                {
                    if (!entities.Contains(subItem))
                    {
                        entities.Add(subItem);
                        Read(subItem.Value as Dictionary<string, object>, ref entities);
                    }
                }
                else
                {
                    entities.Add(subItem);
                }
            }
        }

        public class StackContainerException : Exception
        {
            public StackContainerException() : base()
            {

            }

            public StackContainerException(string format, params object[] args) : base(string.Format(format, args))
            {

            }
        }
    }

   
}
