using UnityEngine;

namespace Narthex.Save
{
    public static class SaveSerializer
    {
        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new SaveData();
            return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        }
    }
}
