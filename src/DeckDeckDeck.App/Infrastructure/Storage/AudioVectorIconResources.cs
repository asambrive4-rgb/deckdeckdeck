// 역할: 사용자의 실물 기기(네이비 헤드폰, 실버 버즈3 프로, 그레이 스피커)의 외형과 고유 색상을 반영한 Bibata 스타일 볼드 벡터 SVG 아이콘을 생성합니다.
using System.Windows.Media;

namespace DeckDeckDeck.App.Infrastructure.Storage;

/// <summary>
/// 오디오 기기 3종(헤드폰, 이어폰, 스피커) 전용 Bibata 스타일 벡터 아이콘 리소스.
/// 사용자의 실물 기기 컬러(XM5 네이비, 버즈3 프로 실버, Flip 4 회색)와 고유 디자인을 정밀하게 반영합니다.
/// </summary>
public static class AudioVectorIconResources
{
    public const string Prefix = "resource:Deck.Icon.Audio.";

    // 오디오 기기 3종 ON/OFF 리소스 키
    public const string HeadphonesOn = Prefix + "Headphones.On";
    public const string HeadphonesOff = Prefix + "Headphones.Off";
    public const string EarbudsOn = Prefix + "Earbuds.On";
    public const string EarbudsOff = Prefix + "Earbuds.Off";
    public const string SpeakerOn = Prefix + "Speaker.On";
    public const string SpeakerOff = Prefix + "Speaker.Off";

    private static readonly IReadOnlyDictionary<string, ImageSource> Cache = CreateIconMap();

    public static bool TryGetVectorIcon(string key, out ImageSource? image)
    {
        return Cache.TryGetValue(key, out image);
    }

    private static IReadOnlyDictionary<string, ImageSource> CreateIconMap()
    {
        return new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase)
        {
            [HeadphonesOn] = CreateHeadphonesIcon(isOn: true),
            [HeadphonesOff] = CreateHeadphonesIcon(isOn: false),
            [EarbudsOn] = CreateEarbudsIcon(isOn: true),
            [EarbudsOff] = CreateEarbudsIcon(isOn: false),
            [SpeakerOn] = CreateSpeakerIcon(isOn: true),
            [SpeakerOff] = CreateSpeakerIcon(isOn: false)
        };
    }

    /// <summary>
    /// 소니 WH-1000XM5 (미드나잇 블루 / 네이비): 슬림 튜블러 밴드 + 골드 힌지 포인트 + 매끈한 타원형 이어컵
    /// </summary>
    private static DrawingImage CreateHeadphonesIcon(bool isOn)
    {
        // 48x48 캔버스 기준: 4~44px 안전 영역 내 배치하여 상하좌우 잘림 완전 차단
        var group = new DrawingGroup();

        var strokeColor = isOn ? Color.FromRgb(0x1E, 0x1B, 0x18) : Color.FromRgb(0x64, 0x74, 0x8B);
        var pen = CreatePen(strokeColor, 2.2);

        // 네이비 바디 컬러 (ON: 딥 매트 네이비 #1C2B44, OFF: 뮤트 슬레이트 #4B5563)
        var navyBodyColor = isOn ? Color.FromRgb(0x1C, 0x2B, 0x44) : Color.FromRgb(0x4B, 0x55, 0x63);
        var navyBodyBrush = new SolidColorBrush(navyBodyColor);
        navyBodyBrush.Freeze();

        // 이어패드 안쪽 섀도우 (더 깊은 딥 네이비 / 다크 슬레이트)
        var innerShadowColor = isOn ? Color.FromRgb(0x12, 0x1C, 0x2E) : Color.FromRgb(0x33, 0x41, 0x55);
        var innerShadowBrush = new SolidColorBrush(innerShadowColor);
        innerShadowBrush.Freeze();

        // 1. XM5 시그니처: 슬림 튜블러 헤드밴드 아치
        var headband = Geometry.Parse("M 13,27 C 13,12.5 35,12.5 35,27");
        var headbandPen = CreatePen(strokeColor, 2.4);
        group.Children.Add(new GeometryDrawing(null, headbandPen, headband));

        // 2. 샴페인 골드 힌지 / 슬라이더 조절 포인트 (XM5의 우아한 디테일)
        var hingeColor = isOn ? Color.FromRgb(0xC5, 0xA8, 0x80) : Color.FromRgb(0x94, 0xA3, 0xB8);
        var hingePen = CreatePen(hingeColor, 2.0);
        var hinges = Geometry.Parse("M 13,23 L 13,26.5 M 35,23 L 35,26.5");
        group.Children.Add(new GeometryDrawing(null, hingePen, hinges));

        // 3. 좌측 타원형 이어컵 (네이비 채움 + 볼드 테두리)
        var leftCup = Geometry.Parse("M 13,26 C 9.5,26 8.5,29.5 8.5,33.5 C 8.5,37.5 9.5,41 13,41 C 16.5,41 17.5,37.5 17.5,33.5 C 17.5,29.5 16.5,26 13,26 Z");
        group.Children.Add(new GeometryDrawing(navyBodyBrush, pen, leftCup));

        // 좌측 이어패드 안쪽 타원 음영
        var leftInner = Geometry.Parse("M 13,29 C 11.2,29 10.5,31 10.5,33.5 C 10.5,36 11.2,38 13,38 C 14.8,38 15.5,36 15.5,33.5 C 15.5,31 14.8,29 13,29 Z");
        group.Children.Add(new GeometryDrawing(innerShadowBrush, null, leftInner));

        // 4. 우측 타원형 이어컵 (네이비 채움 + 볼드 테두리)
        var rightCup = Geometry.Parse("M 35,26 C 31.5,26 30.5,29.5 30.5,33.5 C 30.5,37.5 31.5,41 35,41 C 38.5,41 39.5,37.5 39.5,33.5 C 39.5,29.5 38.5,26 35,26 Z");
        group.Children.Add(new GeometryDrawing(navyBodyBrush, pen, rightCup));

        // 우측 이어패드 안쪽 타원 음영
        var rightInner = Geometry.Parse("M 35,29 C 33.2,29 32.5,31 32.5,33.5 C 32.5,36 33.2,38 35,38 C 36.8,38 37.5,36 37.5,33.5 C 37.5,31 36.8,29 35,29 Z");
        group.Children.Add(new GeometryDrawing(innerShadowBrush, null, rightInner));

        // 5. 상태 뱃지 (안전 여백 준수: cx=39, cy=9)
        AddStatusBadge(group, isOn);

        group.Freeze();
        var img = new DrawingImage(group);
        img.Freeze();
        return img;
    }

    /// <summary>
    /// 삼성 갤럭시 버즈3 프로 실버 (Galaxy Buds3 Pro Silver): 각진 블레이드 콩나물 스템 + 세로 블레이드 라이트(LED) + 메탈릭 실버
    /// </summary>
    private static DrawingImage CreateEarbudsIcon(bool isOn)
    {
        var group = new DrawingGroup();

        var strokeColor = isOn ? Color.FromRgb(0x1E, 0x1B, 0x18) : Color.FromRgb(0x64, 0x74, 0x8B);
        var pen = CreatePen(strokeColor, 2.2);

        // 메탈릭 실버 바디 컬러 (ON: 빛나는 실버 #E2E8F0, OFF: 매트 라이트 그레이 #CBD5E1)
        var silverColor = isOn ? Color.FromRgb(0xE2, 0xE8, 0xF0) : Color.FromRgb(0xCB, 0xD5, 0xE1);
        var silverBrush = new SolidColorBrush(silverColor);
        silverBrush.Freeze();

        // 각진 블레이드 스템 섀도우 면 (ON: 짙은 크롬 섀도우 #94A3B8, OFF: 슬레이트 #64748B)
        var bladeShadowColor = isOn ? Color.FromRgb(0x94, 0xA3, 0xB8) : Color.FromRgb(0x64, 0x74, 0x8B);
        var bladeShadowBrush = new SolidColorBrush(bladeShadowColor);
        bladeShadowBrush.Freeze();

        // 커널형 실리콘 이어팁 컬러 (ON: 반투명 다크 실버 그레이 #64748B, OFF: #94A3B8)
        var tipColor = isOn ? Color.FromRgb(0x64, 0x74, 0x8B) : Color.FromRgb(0x94, 0xA3, 0xB8);
        var tipBrush = new SolidColorBrush(tipColor);
        tipBrush.Freeze();

        // 1. 좌측 버즈3 프로 (커널 헤드 + 각진 블레이드 스템)
        // 1-1. 안쪽 인이어 실리콘 이어팁
        var leftTip = Geometry.Parse("M 11,19 C 8.5,19 7.5,21.5 7.5,23.5 C 7.5,25.5 8.5,27 11,27 Z");
        group.Children.Add(new GeometryDrawing(tipBrush, CreatePen(strokeColor, 1.5), leftTip));

        // 1-2. 상단 인체공학 이어버드 헤드 하우징
        var leftHead = Geometry.Parse("M 14,14 C 10.5,14 10,18 10.5,22 C 11,25 13.5,26 15.5,25 C 18,24 19,20 18.5,17 C 18,14.5 16,14 14,14 Z");
        group.Children.Add(new GeometryDrawing(silverBrush, pen, leftHead));

        // 1-3. 날렵한 삼각기둥 블레이드 스템 (좌측 밝은 면)
        var leftBladeFaceLight = Geometry.Parse("M 12.5,24.5 L 12.5,39.5 L 15,40.5 L 15,24.5 Z");
        group.Children.Add(new GeometryDrawing(silverBrush, pen, leftBladeFaceLight));

        // 1-4. 날렵한 삼각기둥 블레이드 스템 (우측 각진 섀도우 면 - 사이버틱 엣지)
        var leftBladeFaceShadow = Geometry.Parse("M 15,24.5 L 15,40.5 L 17.5,39 L 17.5,23.5 Z");
        group.Children.Add(new GeometryDrawing(bladeShadowBrush, pen, leftBladeFaceShadow));

        // 1-5. 버즈3 프로 시그니처: 세로 블레이드 라이트 (Blade Light LED Bar)
        var leftBladeLight = Geometry.Parse("M 15,26 L 15,38");
        var ledColor = isOn ? Color.FromRgb(0x38, 0xBD, 0xF8) : Color.FromRgb(0x64, 0x74, 0x8B); // ON: 빛나는 아이스 블루 네온, OFF: 꺼진 슬레이트
        var ledPen = CreatePen(ledColor, isOn ? 1.6 : 1.2);
        group.Children.Add(new GeometryDrawing(null, ledPen, leftBladeLight));

        // 2. 우측 버즈3 프로 (대칭 조형)
        // 2-1. 안쪽 인이어 실리콘 이어팁
        var rightTip = Geometry.Parse("M 37,19 C 39.5,19 40.5,21.5 40.5,23.5 C 40.5,25.5 39.5,27 37,27 Z");
        group.Children.Add(new GeometryDrawing(tipBrush, CreatePen(strokeColor, 1.5), rightTip));

        // 2-2. 상단 인체공학 이어버드 헤드 하우징
        var rightHead = Geometry.Parse("M 34,14 C 37.5,14 38,18 37.5,22 C 37,25 34.5,26 32.5,25 C 30,24 29,20 29.5,17 C 30,14.5 32,14 34,14 Z");
        group.Children.Add(new GeometryDrawing(silverBrush, pen, rightHead));

        // 2-3. 날렵한 삼각기둥 블레이드 스템 (좌측 섀도우 면)
        var rightBladeFaceShadow = Geometry.Parse("M 30.5,23.5 L 30.5,39 L 33,40.5 L 33,24.5 Z");
        group.Children.Add(new GeometryDrawing(bladeShadowBrush, pen, rightBladeFaceShadow));

        // 2-4. 날렵한 삼각기둥 블레이드 스템 (우측 밝은 면)
        var rightBladeFaceLight = Geometry.Parse("M 33,24.5 L 33,40.5 L 35.5,39.5 L 35.5,24.5 Z");
        group.Children.Add(new GeometryDrawing(silverBrush, pen, rightBladeFaceLight));

        // 2-5. 우측 세로 블레이드 라이트 (Blade Light LED Bar)
        var rightBladeLight = Geometry.Parse("M 33,26 L 33,38");
        group.Children.Add(new GeometryDrawing(null, ledPen, rightBladeLight));

        // 3. 상태 뱃지
        AddStatusBadge(group, isOn);

        group.Freeze();
        var img = new DrawingImage(group);
        img.Freeze();
        return img;
    }

    /// <summary>
    /// JBL Flip 4 (회색): 텀블러 실린더 바디 + 어반 그레이 패브릭 메쉬 + 주황색 메탈 JBL 로고 뱃지
    /// </summary>
    private static DrawingImage CreateSpeakerIcon(bool isOn)
    {
        var group = new DrawingGroup();

        var strokeColor = isOn ? Color.FromRgb(0x1E, 0x1B, 0x18) : Color.FromRgb(0x64, 0x74, 0x8B);
        var pen = CreatePen(strokeColor, 2.2);

        // 회색 바디 컬러 (ON: 어반 매트 그레이 #5C6470, OFF: 페일 그레이 #94A3B8)
        var grayBodyColor = isOn ? Color.FromRgb(0x5C, 0x64, 0x70) : Color.FromRgb(0x94, 0xA3, 0xB8);
        var grayBodyBrush = new SolidColorBrush(grayBodyColor);
        grayBodyBrush.Freeze();

        // 양 끝 패시브 라디에이터 캡 (ON: 딥 차콜 #333A44, OFF: 쿨 그레이 #64748B)
        var capColor = isOn ? Color.FromRgb(0x33, 0x3A, 0x44) : Color.FromRgb(0x64, 0x74, 0x8B);
        var capBrush = new SolidColorBrush(capColor);
        capBrush.Freeze();

        // 1. 원통형 메인 바디 (안전 여백 준수: X=11~37, Y=16~34)
        var body = Geometry.Parse("M 13,16 L 35,16 C 37,16 38,17.5 38,20 L 38,30 C 38,32.5 37,34 35,34 L 13,34 C 11,34 10,32.5 10,30 L 10,20 C 10,17.5 11,16 13,16 Z");
        group.Children.Add(new GeometryDrawing(grayBodyBrush, pen, body));

        // 2. 그릴 텍스처 스트라이프 라인
        var grillColor = isOn ? Color.FromArgb(0x40, 0x00, 0x00, 0x00) : Color.FromArgb(0x25, 0x00, 0x00, 0x00);
        var grillPen = CreatePen(grillColor, 1.4);
        var grillLines = Geometry.Parse("M 15,18 L 15,32 M 19,17.5 L 19,32.5 M 29,17.5 L 29,32.5 M 33,18 L 33,32");
        group.Children.Add(new GeometryDrawing(null, grillPen, grillLines));

        // 3. 양 끝 패시브 라디에이터 캡 (외곽 돌출 범위를 48x48 내부로 안전 마감)
        var leftCap = Geometry.Parse("M 10,19 C 8.2,20.5 8.2,29.5 10,31");
        var rightCap = Geometry.Parse("M 38,19 C 39.8,20.5 39.8,29.5 38,31");
        var capPen = CreatePen(strokeColor, 2.0);
        group.Children.Add(new GeometryDrawing(null, capPen, leftCap));
        group.Children.Add(new GeometryDrawing(null, capPen, rightCap));

        // 4. 중앙 아이코닉 오렌지색 JBL 메탈 로고 뱃지
        var jblOrangeColor = isOn ? Color.FromRgb(0xFF, 0x57, 0x22) : Color.FromRgb(0x94, 0xA3, 0xB8);
        var jblOrangeBrush = new SolidColorBrush(jblOrangeColor);
        jblOrangeBrush.Freeze();
        var logoBox = Geometry.Parse("M 21.5,22.5 H 26.5 V 27.5 H 21.5 Z");
        group.Children.Add(new GeometryDrawing(jblOrangeBrush, CreatePen(strokeColor, 1.2), logoBox));

        // 로고 내부 미니 심볼 포인트 (화이트 느낌)
        var logoDot = Geometry.Parse("M 23,24.5 H 25");
        group.Children.Add(new GeometryDrawing(null, CreatePen(Colors.White, 1.0), logoDot));

        // 5. 상태 뱃지
        AddStatusBadge(group, isOn);

        group.Freeze();
        var img = new DrawingImage(group);
        img.Freeze();
        return img;
    }

    /// <summary>
    /// 안전 여백(Safe Padding)을 완벽하게 준수하여 잘림 없이 렌더링되는 우측 상단 상태 뱃지
    /// </summary>
    private static void AddStatusBadge(DrawingGroup group, bool isOn)
    {
        // 캔버스 우측 상단 (cx=39, cy=9, r=4.5) -> 외곽 끝 좌표 43.5로 48x48 경계 안쪽에 완벽 안착!
        var badgeCircle = Geometry.Parse("M 39,4.5 A 4.5,4.5 0 1 0 39,4.51");

        if (isOn)
        {
            // ON: 선명한 에메랄드 그린 + 화이트 체크
            var badgeBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            badgeBrush.Freeze();
            group.Children.Add(new GeometryDrawing(badgeBrush, null, badgeCircle));

            var check = Geometry.Parse("M 36.8,9 L 38.3,10.5 L 41.5,7");
            var checkPen = CreatePen(Colors.White, 1.4);
            group.Children.Add(new GeometryDrawing(null, checkPen, check));
        }
        else
        {
            // OFF: 차분한 슬레이트 그레이 + 화이트 마이너스
            var badgeBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            badgeBrush.Freeze();
            group.Children.Add(new GeometryDrawing(badgeBrush, null, badgeCircle));

            var minus = Geometry.Parse("M 36.8,9 H 41.2");
            var minusPen = CreatePen(Colors.White, 1.4);
            group.Children.Add(new GeometryDrawing(null, minusPen, minus));
        }
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }
}
