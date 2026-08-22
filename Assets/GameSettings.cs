using System;

[Serializable]
public class GameSettings
{
    public float masterVolume = 1f;
    public float sharpness = 1f;   // 선명도 - "화면" 탭으로 옮길지 미정이라 우선 볼륨 탭 항목으로 유지
    public float bgmVolume = 1f;
    public float sfxVolume = 1f;
}
