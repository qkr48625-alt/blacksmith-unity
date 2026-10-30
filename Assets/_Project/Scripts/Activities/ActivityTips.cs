using System.Collections.Generic;
using Blacksmith.Crafting;

namespace Blacksmith.Activities
{
    // 작업대에서 일하는 동안 왼쪽 위에 띄우는 짧은 요령(조작은 아래 안내 줄에 따로 나온다)
    public static class ActivityTips
    {
        static readonly Dictionary<string, string> ById = new()
        {
            ["smelt.bellows"] = "먼저 바늘을 금색 칸까지 올리세요. 닿는 순간부터 30초가 흐릅니다.\n칸이 깜빡이고 '띵' 하면 곧 옮겨 갑니다 — 미리 바람을 넣거나 멈춰 두세요.\n불은 바람을 멈춰도 바로 식지 않습니다. 칸 위쪽에 닿기 전에 손을 떼세요.",
            ["smelt.slag"] = "흰 표면 선이 금색 선에 닿는 순간 찌르세요.\n뚫을 때마다 금색 선 높이가 바뀌고, 차오르는 속도도 빨라집니다.\n송풍관(오른쪽 관)까지 차면 0점입니다.",
            ["fold.fold"] = "스페이스를 누르고 있으면 게이지가 오르고, 위로 갈수록 빨라집니다.\n맨 위 얇은 금색 선에서 떼면 최대 힘! 넘기면 아래부터 다시 오릅니다.\n쇠가 체리빛 아래로 식으면 맞아도 반만 먹습니다 — 화덕에 다녀오세요.",
            ["fold.sort"] = "강(날감): 끝이 곧고, 단면에 작은 반짝임이 고르게.\n연철(몸통감): 끝이 휘고, 단면에 긴 섬유 줄 · 검은 슬래그 줄.\n뒤로 갈수록 단서가 흐려집니다. 빨리 맞힐수록 점수가 높아요.",
            ["shape.form"] = "고리는 저절로 떨립니다. 떨림이 표적 위를 지나는 순간을 노려 클릭하세요.\n표적이 작아지기 전에(3.6초) 쳐야 합니다.\n쇠가 식으면 감점 — 퀘스트 창이 화덕을 가리키면 다녀오세요.",
            ["shape.mark"] = "첫 번째는 점선 원 가운데, 두 번째는 첫 자국 바로 위에.\n쇠가 뜨거울 때(밝을 때) 찍어야 깊고 또렷합니다.",
            ["shape.normalize"] = "옆 견본(금 테)과 같은 체리빛이 되면 떼세요.\n검게 식을 때까지 기다렸다 다시 달구세요. 서두르면 그 회차가 깎입니다.",
            ["temper.quench"] = "칼날이 옆 견본(체리빛)과 같은 색일 때 담그세요.\n너무 뜨거우면 기다리고, 너무 식었으면 화덕에서 다시 달궈 오세요.",
            ["temper.temper"] = "색은 짚색 → 갈색 → 보라 → 청색으로 번집니다.\n금 테를 두른 견본 색에 닿는 순간 집어 올리세요.",
            ["grind.grind"] = "숫돌 가운데를 중심으로 크게 원을 그리세요. '알맞은 속도'를 유지!\n빨리 돌리면 칼날이 달아올라 푸르게 변합니다 — 스페이스/우클릭으로 물.",
            ["grind.hone"] = "빛나는 원에서 눌러 잡고, 금빛 날 선을 따라 끝까지 한 번에.\n너무 빨리 밀면(0.45초 안) 날이 뭉개져 감점됩니다.",
            ["handle.awl"] = "송곳 끝은 저절로 옆으로 흐릅니다. 흐르는 반대쪽으로 마우스를 움직여 금빛 선 위에.\n깊이 들어갈수록 크게 흔들립니다.",
            ["handle.assemble"] = "순서: 아래 론델 → 그립 → 위 론델 → 폼멜. 쐐기는 필요 없습니다.\n금 테 · 굵은 쪽은 날 쪽(왼쪽), 위 론델만 금 테가 바깥쪽. R/우클릭으로 뒤집기.",
            ["handle.rivet"] = "접기와 같은 망치질. 금색 띠 높이에서 떼세요.\n너무 세게 치면 슴베가 휩니다.",
            ["sheath.form"] = "점선 윤곽을 따라 문지르세요. 선 밖을 문지르면 정확도가 떨어집니다.\n가죽이 마르면(밝아지면) 더는 모양이 잡히지 않습니다.",
            ["sheath.stamp"] = "금빛 가운데 선을 기준으로 좌우 대칭이 되게 짝지어 찍으세요.\n간격을 고르게, 입구부터 끝까지 퍼지게. 6개 이상이면 만점 가능. Enter 로 마치기.",
            ["sheath.stitch"] = "붉은 구슬 = 찌르기(좌클릭/A), 푸른 구슬 = 당기기(우클릭/D).\n구슬이 금빛 고리 한가운데 닿을 때. 점점 빨라집니다.",
        };

        static readonly Dictionary<StationKind, string> ByStation = new()
        {
            [StationKind.Forge] = "쇠를 달구려면 '쇠 달구기' — 풀무질로 불을 키우면 빨리 달궈집니다.\n하얗게 빛나면 타기 시작합니다! 오래 타면 작업물을 버려야 해요.",
            [StationKind.Anvil] = "모루 일은 달군 쇠로. 체리빛 아래면 점수가 깎입니다.",
            [StationKind.Quench] = "담금질은 체리빛일 때. 화덕에서 달궈 오세요.",
            [StationKind.Grind] = "숫돌은 담금질 · 뜨임이 끝난 칼날로.",
            [StationKind.Bench] = "손잡이 · 칼집 일은 쇠가 식어도 괜찮습니다.",
        };

        public static string Of(string partId) => partId != null && ById.TryGetValue(partId, out var t) ? t : null;
        public static string Of(StationKind kind) => ByStation.TryGetValue(kind, out var t) ? t : null;
    }
}
