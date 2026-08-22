using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;
    public const int SlotCount = 3;

    // 타이틀에서 슬롯을 고른 뒤, 게임 씬이 이어서 읽어갈 현재 세이브 데이터
    public SaveData ActiveSave { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private string GetSlotPath(int slotIndex) => Path.Combine(Application.persistentDataPath, $"save_{slotIndex}.json");

    public bool HasSave(int slotIndex) => File.Exists(GetSlotPath(slotIndex));

    public SaveData Load(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
    }

    public void Save(int slotIndex, SaveData data)
    {
        data.slotIndex = slotIndex;
        data.timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
        File.WriteAllText(GetSlotPath(slotIndex), JsonUtility.ToJson(data, true));
    }

    public void DeleteSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
    }

    public void SetActiveSave(SaveData data)
    {
        ActiveSave = data;
    }
}
