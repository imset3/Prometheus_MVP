using System;
using System.Collections.Generic;

namespace Narthex.Save
{
    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        void Apply(SaveData data);
    }

    public sealed class MigrationRunner
    {
        private readonly List<ISaveMigration> migrations = new List<ISaveMigration>();

        public void Register(ISaveMigration migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            migrations.Add(migration);
        }

        public void Run(SaveData data, int targetVersion)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            while (data.Permanent.SaveVersion < targetVersion)
            {
                var migration = migrations.Find(item => item.FromVersion == data.Permanent.SaveVersion);
                if (migration == null)
                    throw new InvalidOperationException($"Missing save migration from version {data.Permanent.SaveVersion}");

                migration.Apply(data);
                data.Permanent.SaveVersion = migration.ToVersion;
            }
        }
    }
}
