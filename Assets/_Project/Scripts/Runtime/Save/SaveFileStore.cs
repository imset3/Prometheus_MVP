using System;
using System.IO;

namespace Narthex.Save
{
    public sealed class SaveFileStore
    {
        private readonly string filePath;

        public SaveFileStore(string filePath)
        {
            this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public SaveData Load()
        {
            if (!File.Exists(filePath)) return new SaveData();

            try
            {
                return SaveSerializer.FromJson(File.ReadAllText(filePath));
            }
            catch (Exception)
            {
                return new SaveData();
            }
        }

        public void Save(SaveData data)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, SaveSerializer.ToJson(data));

            if (File.Exists(filePath)) File.Replace(tempPath, filePath, null);
            else File.Move(tempPath, filePath);
        }
    }
}
