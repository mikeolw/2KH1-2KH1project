using System;

// 환경설정 값 모음. 세이브 슬롯과 달리 슬롯 구분 없이 기기(플레이어)당 1개만 존재하며,
// SettingsManager가 PlayerPrefs에 JSON 문자열로 통째로 저장/로드한다.
// 값 범위는 전부 0~1 (슬라이더 min/max와 맞춤).
[Serializable]
public class GameSettings
{
    // 마스터 볼륨. SettingsManager.Apply()에서 AudioListener.volume에 바로 반영된다.
    public float masterVolume = 1f;

    // "선명도" - 환경설정 목업(PDF)에는 볼륨 탭 안에 같이 그려져 있었지만, 성격상
    // 화면(디스플레이) 설정에 더 가깝다. 화면 탭 내용이 확정되면 그쪽으로 옮길지 결정 필요.
    // TODO: 실제로 화면 밝기/감마 등에 적용하는 코드가 없다. 지금은 값만 저장된다.
    public float sharpness = 1f;

    // 배경음악(BGM) 볼륨. TODO: 이 값을 실제로 적용할 BGM 전용 AudioSource/AudioMixer가
    // 프로젝트에 아직 없다. DialogueLine.bgmToPlay를 재생하는 코드가 생기면, 그 AudioSource의
    // volume을 SettingsManager.Current.bgmVolume에 연동해야 한다.
    public float bgmVolume = 1f;

    // 효과음(SFX) 볼륨. TODO: bgmVolume과 마찬가지로 아직 적용할 대상이 없다.
    // DialogueLine.sfxToPlay 재생용 AudioSource가 생기면 여기에 연결.
    public float sfxVolume = 1f;
}
