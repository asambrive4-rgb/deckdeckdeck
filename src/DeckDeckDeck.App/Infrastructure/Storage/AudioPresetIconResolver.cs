// 역할: 오디오 기기 3종(헤드폰, 이어폰, 스피커)의 터미널 명령 및 기기명을 분석하여 알맞은 벡터 아이콘을 매핑합니다.
using System.Windows.Media;
using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.Infrastructure.Storage;

/// <summary>
/// 오디오 기기 3종(스피커, 이어폰, 헤드폰) 전용 아이콘 리졸버.
/// 오직 오디오 3대 기기에 대해서만 벡터 아이콘을 매핑하며, 그 외의 기능(모니터 절전 등)은 기존 텍스트 화면을 유지합니다.
/// </summary>
public static class AudioPresetIconResolver
{
    public static bool TryGetImage(string key, out ImageSource? image)
    {
        return AudioVectorIconResources.TryGetVectorIcon(key, out image);
    }

    /// <summary>
    /// 터미널 명령 및 제목을 분석하여 오디오 3종 기기(헤드폰/이어폰/스피커)에 해당하는 경우에만 아이콘 리소스 경로를 반환합니다.
    /// </summary>
    public static string? GetAutoIconResourcePath(string? terminalCommand, string? title)
    {
        var textToAnalyze = $"{title} {terminalCommand}".Trim();
        if (string.IsNullOrWhiteSpace(textToAnalyze))
        {
            return null;
        }

        var lower = textToAnalyze.ToLowerInvariant();

        // 1. 오디오 외의 기능(모니터 절전, 핫스팟 등)은 아이콘을 씌우지 않고 기존 방식(텍스트 표시) 유지
        if (lower.Contains("절전") || lower.Contains("화면 끄기") || lower.Contains("0x0112") || lower.Contains("핫스팟") || lower.Contains("hotspot"))
        {
            return null;
        }

        // 2. 오디오 기기 판별 (헤드폰, 이어폰, 스피커)
        var deviceType = BluetoothAudioStatusRules.DetermineAudioDeviceType(textToAnalyze);
        if (deviceType == BluetoothAudioDeviceType.Unknown)
        {
            if (lower.Contains("buds") || lower.Contains("버즈") || lower.Contains("이어폰"))
            {
                deviceType = BluetoothAudioDeviceType.Earbuds;
            }
            else if (lower.Contains("xm5") || lower.Contains("xm4") || lower.Contains("wh-") || lower.Contains("헤드폰") || lower.Contains("헤드셋"))
            {
                deviceType = BluetoothAudioDeviceType.Headphones;
            }
            else if (lower.Contains("flip") || lower.Contains("jbl") || lower.Contains("speaker") || lower.Contains("스피커"))
            {
                deviceType = BluetoothAudioDeviceType.Speaker;
            }
        }

        // 오디오 3종에 해당하지 않으면 null 반환 (기존 텍스트 레이아웃 유지)
        if (deviceType == BluetoothAudioDeviceType.Unknown)
        {
            return null;
        }

        var isOff = lower.Contains("disconnect") || lower.Contains("off") || lower.Contains("해제") || lower.Contains("끄기");

        return deviceType switch
        {
            BluetoothAudioDeviceType.Headphones => isOff ? AudioVectorIconResources.HeadphonesOff : AudioVectorIconResources.HeadphonesOn,
            BluetoothAudioDeviceType.Earbuds => isOff ? AudioVectorIconResources.EarbudsOff : AudioVectorIconResources.EarbudsOn,
            BluetoothAudioDeviceType.Speaker => isOff ? AudioVectorIconResources.SpeakerOff : AudioVectorIconResources.SpeakerOn,
            _ => null
        };
    }
}
