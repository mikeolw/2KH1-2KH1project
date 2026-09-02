using System;

// 환경설정 값 모음. 세이브 슬롯과 달리 슬롯 구분 없이 기기(플레이어)당 1개만 존재하며,
// SettingsManager가 PlayerPrefs에 JSON 문자열로 통째로 저장/로드한다.
// 값 범위는 전부 0~1 (슬라이더 min/max와 맞춤).
[Serializable]
public class GameSettings
{
    // 마스터 볼륨. SettingsManager.Apply()에서 AudioListener.volume에 바로 반영된다.
    public float masterVolume = 1f;

    // 배경음악(BGM) 볼륨. TODO: 이 값을 실제로 적용할 BGM 전용 AudioSource/AudioMixer가
    // 프로젝트에 아직 없다. DialogueLine.bgmToPlay를 재생하는 코드가 생기면, 그 AudioSource의
    // volume을 SettingsManager.Current.bgmVolume에 연동해야 한다.
    public float bgmVolume = 1f;

    // 효과음(SFX) 볼륨. TODO: bgmVolume과 마찬가지로 아직 적용할 대상이 없다.
    // DialogueLine.sfxToPlay 재생용 AudioSource가 생기면 여기에 연결.
    public float sfxVolume = 1f;

    // 창 모드 여부. true면 SettingsManager.Apply()가 Screen.SetResolution을
    // 1080x810(FullScreenMode.Windowed)으로 호출하고, false면 프로젝트 기본값인
    // 1920x1080(FullScreenMode.FullScreenWindow)으로 되돌린다.
    // 에디터 Game 뷰에서는 Screen.SetResolution이 아무 효과가 없으므로(빌드에서만 동작),
    // 실제 창 크기 변화는 빌드에서만 확인 가능하다.
    public bool windowed = false;
}
