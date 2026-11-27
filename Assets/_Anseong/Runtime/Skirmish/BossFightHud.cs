using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 보스전 화면 표시(기획안 2026-11-27 "화면 표시"). 숫자는 쓰지 않는다.
    /// 줄과 칸은 구석이 아니라 시야 가운데에서 15~20도 안쪽에 둔다. 색은 넷: 붉은색 = 막아라·위험, 노란색 = 피해라,
    /// 초록색 = 기회(때릴 틈, 약점), 흰색 = 상태.
    /// 두 사람 모두: 보스 체력 줄(P1 아래 가운데, P2 위 가운데), 그 밑의 줄어드는 초록 줄(때릴 틈), 자막 한 줄.
    /// P1: 막을 쪽의 붉은 호(시야 왼쪽·오른쪽 가장자리), 조종석 왼쪽 아래의 로봇 상태판(선 색이 내구도).
    /// P2: 전부 조준점에. 둘레 고리 = 과열, 옆 칸 셋 = 강한 탄, 바깥 고리 = 스캔, 약점에는 마름모와 줄어드는 고리.
    /// 화면은 시작할 때 코드로 만든다. 헤드셋에서는 머리가 아니라 자리에 고정한다(눈앞 distance).
    /// </summary>
    public class BossFightHud : MonoBehaviour
    {
        [SerializeField, Tooltip("XR Origin 루트(헤드셋일 때 여기에 붙여 자리에 고정한다).")] Transform rig;
        [SerializeField, Tooltip("머리(모니터 확인일 때는 머리에 붙인다).")] Transform head;
        [SerializeField, Tooltip("한글이 나오는 OS 글꼴(앞에서부터 있는 것).")] string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };
        [SerializeField, Min(0.5f), Tooltip("헤드셋: 눈앞 이 거리에 띄운다(m).")] float distance = 2.5f;
        [SerializeField, Range(0.2f, 1f), Tooltip("전체 비침(1 = 불투명).")] float opacity = 0.85f;
        [SerializeField, Range(0.1f, 1f), Tooltip("보스 체력 줄이 평소에 흐린 정도. 값이 바뀌면 잠깐 밝아진다.")] float idleAlpha = 0.5f;
        [SerializeField, Min(0f), Tooltip("값이 바뀐 뒤 밝게 남아 있는 시간(초).")] float brightSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("깎인 만큼이 밝게 남아 있는 시간(초).")] float ghostHold = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("남아 있던 부분이 줄어드는 빠르기(1초에 줄 전체의 몇 배).")] float ghostSpeed = 1.2f;
        [SerializeField, Tooltip("보스 체력 줄의 높이(캔버스 칸, 1칸은 약 0.057도). P1은 아래로, P2는 위로 이만큼.")] float bossBarOffset = 300f;
        [SerializeField, Tooltip("P1 자막의 높이(캔버스 칸, 아래가 음수). P2는 보스 체력 줄 위.")] float subtitleY = -215f;
        [SerializeField, Tooltip("로봇 내구도가 이 비율 아래면 상태판이 노란색.")] float warnBelow = 0.6f;
        [SerializeField, Tooltip("로봇 내구도가 이 비율 아래면 상태판이 붉게 깜빡인다.")] float dangerBelow = 0.3f;
        [SerializeField, Min(0f), Tooltip("맞은 부위가 번쩍이는 시간(초).")] float flashSeconds = 0.3f;
        [SerializeField, Min(0f), Tooltip("잃은 방패와 검이 붉게 남아 있다가 꺼지는 시간(초).")] float gearOffSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("맞았을 때 화면 표시가 흔들리는 폭(캔버스 칸). 시야는 흔들지 않는다.")] float shake = 10f;
        [SerializeField] Color redColor = new Color(0.86f, 0.12f, 0.08f);
        [SerializeField] Color yellowColor = new Color(1f, 0.8f, 0.15f);
        [SerializeField] Color greenColor = new Color(0.3f, 0.95f, 0.45f);
        [SerializeField] Color whiteColor = new Color(0.92f, 0.9f, 0.84f);
        [SerializeField] Color ghostColor = new Color(1f, 0.82f, 0.6f);
        [SerializeField] Color backColor = new Color(0f, 0f, 0f, 0.55f);

        // 캔버스는 1600×900 칸. 1m 앞에서 가로 1.6m로 보이게 줄여 쓴다. 가운데에서 300칸이 약 17도.
        const float Width = 1600f, Height = 900f, BossWidth = 600f;

        enum Part { Head, Body, LeftArm, RightArm, Legs }

        RectTransform canvas, root, reticle, weak;
        CanvasGroup group, bossAlpha;
        Image bossFill, bossGhost, openFill, heatRing, scanRing, weakRing, arcLeft, arcRight;
        Image[] shots;
        readonly List<Image>[] partLines = new List<Image>[5];
        readonly Image[] partFlash = new Image[5];
        readonly float[] flash = new float[5];
        GameObject bossGroup, statusPanel;
        Text message, subtitle;
        Sprite white, ringSprite, thinRing;
        float ghost = 1f, ghostWait, maxHp, lastFraction = 1f, bright, openMax, lastHull = -1f, shakeLeft, subtitleLeft;
        int epoch = -1;
        SkirmishSeat placedSeat;
        bool placed, placedVr;

        /// <summary>P2가 겨누는 방향(월드). 조준점을 여기에 그린다.</summary>
        public Vector3 Aim { get; set; }

        public void Bind(Transform rigRoot, Transform headTransform) { rig = rigRoot; head = headTransform; }

        /// <summary>사령관 대사 한 줄을 두 사람 모두에게 띄운다.</summary>
        public void Say(string line, float seconds = 3f) { pendingLine = line; subtitleLeft = seconds; }
        string pendingLine = "";
        bool saidGuard, saidOpen, saidDrone, saidBroken;
        int saidAct;
        readonly List<Image> shieldLines = new List<Image>(), swordLines = new List<Image>();
        bool gearLost;
        float gearOffIn;

        /// <summary>1막에서 이미 일어나는 일에 붙는 사령관 대사(기획안 대사 표 1-1, 3-1, 3-2). 상태만 보고 띄우므로 두 PC에서 같이 나온다.</summary>
        void Lines(SkirmishState st)
        {
            if (st.phase != SkirmishPhase.Fighting) return;
            if (!saidGuard && st.gruntIndex >= 0 && st.gruntStep == GruntStep.Rise)
            { saidGuard = true; Say("기체, 막아라. 칼이 올라가는 쪽이다. 왼쪽, 오른쪽."); }
            if (!saidOpen && st.gruntIndex >= 0 && st.gruntStep == GruntStep.Stagger)
            { saidOpen = true; Say("열렸다. 지원, 지금이다! 기체, 베어라!"); }
            if (!saidDrone)
                foreach (var shell in st.shells)
                    if (shell.kind == ShellKind.BigShell) { saidDrone = true; Say("동쪽에서 자폭 드론. 지원, 쏴서 떨어뜨려라."); break; }
            // 막이 바뀔 때(대사 표 3-12, 3-13). 체력 줄도 같이 밝힌다.
            if (st.act != saidAct)
            {
                if (st.act > saidAct) bright = brightSeconds;
                if (st.act == 1 && saidAct == 0) Say("지원, 보스를 스캔해라. 약점이 뜨면 강한 탄을 꽂아라. 기체, 그동안 드론을 막아 줘라.", 4f);
                saidAct = st.act;
            }
            if (st.swordBroken && !saidBroken) Say("검과 방패가 나갔다. 기체, 팔로 막아라. 주먹으로 끝낸다.", 4f);
            saidBroken = st.swordBroken;
        }

        Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        RectTransform Group(string name, Transform parent, Vector2 position)
        {
            var rect = new GameObject(name).AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        /// <summary>네 변만 있는 네모(선 그림).</summary>
        void Frame(List<Image> into, string name, Transform parent, Vector2 center, Vector2 size, float line)
        {
            into.Add(Box(name + " top", parent, center + new Vector2(0f, size.y * 0.5f), new Vector2(size.x + line, line), whiteColor));
            into.Add(Box(name + " bottom", parent, center - new Vector2(0f, size.y * 0.5f), new Vector2(size.x + line, line), whiteColor));
            into.Add(Box(name + " left", parent, center - new Vector2(size.x * 0.5f, 0f), new Vector2(line, size.y), whiteColor));
            into.Add(Box(name + " right", parent, center + new Vector2(size.x * 0.5f, 0f), new Vector2(line, size.y), whiteColor));
        }

        /// <summary>왼쪽 끝에서 차오르는 줄. 뒷판, (있으면) 밝은 잔상, 채움 순서로 겹친다.</summary>
        Image Bar(string name, Transform parent, Vector2 leftCenter, Vector2 size, Color color, out Image ghostImage, bool withGhost, bool withBack = true)
        {
            var center = leftCenter + new Vector2(size.x * 0.5f, 0f);
            if (withBack) Box(name + " back", parent, center, size + new Vector2(4f, 4f), backColor);
            ghostImage = withGhost ? Fill(Box(name + " ghost", parent, center, size, ghostColor)) : null;
            return Fill(Box(name, parent, center, size, color));
        }

        Image Fill(Image image)
        {
            // 채움 방식은 스프라이트가 있어야 먹는다. 흰 1칸짜리를 쓴다.
            image.sprite = white;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            return image;
        }

        Image Ring(string name, Transform parent, float size, Sprite sprite, Color color)
        {
            var image = Box(name, parent, Vector2.zero, new Vector2(size, size), color);
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            return image;
        }

        static Sprite MakeRing(int size, float inner, float outer)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Ring", hideFlags = HideFlags.DontSave };
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                float a = Mathf.Clamp01((r - inner) * half * 0.5f) * Mathf.Clamp01((outer - r) * half * 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        void Build()
        {
            var go = new GameObject("Boss fight HUD");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            canvas = (RectTransform)go.transform;
            canvas.sizeDelta = new Vector2(Width, Height);
            group = go.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "White", hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            white = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            ringSprite = MakeRing(256, 0.84f, 0.98f);
            thinRing = MakeRing(1024, 0.962f, 0.992f);
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, 28);
            // 맞았을 때는 이 묶음만 흔든다.
            root = Group("Root", canvas, Vector2.zero);

            // 보스 체력: 길고 얇은 붉은 줄, 800과 400 자리에 가는 눈금, 줄 위 왼쪽에 작은 글씨. 밑에 때릴 틈의 초록 줄.
            var bossRect = Group("Boss", root, Vector2.zero);
            bossGroup = bossRect.gameObject;
            bossAlpha = bossGroup.AddComponent<CanvasGroup>();
            var bossLeft = new Vector2(-BossWidth * 0.5f, 0f);
            bossFill = Bar("Boss hp", bossRect, bossLeft, new Vector2(BossWidth, 8f), redColor, out bossGhost, true);
            foreach (float mark in new[] { 1f / 3f, 2f / 3f })
                Box("Act mark", bossRect, bossLeft + new Vector2(BossWidth * mark, 0f), new Vector2(2f, 18f), whiteColor);
            var label = Label("Boss name", bossRect, bossLeft + new Vector2(120f, 22f), new Vector2(240f, 30f), font, 20, TextAnchor.MiddleLeft);
            label.text = "적 기체";
            openFill = Bar("Open", bossRect, bossLeft + new Vector2(0f, -14f), new Vector2(BossWidth, 5f), greenColor, out _, false, false);

            // P1: 막을 쪽의 붉은 호. 큰 고리의 왼쪽·오른쪽 토막만 그린다.
            arcLeft = Ring("Guard left", root, 1240f, thinRing, redColor);
            arcRight = Ring("Guard right", root, 1240f, thinRing, redColor);
            arcLeft.fillAmount = arcRight.fillAmount = 0.11f;
            // 채움은 위에서 시계 방향으로 40도쯤. 그 토막의 가운데가 왼쪽·오른쪽에 오게 돌린다.
            arcLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 110f);
            arcRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -70f);

            // P1: 조종석 왼쪽 아래의 로봇 상태판. 뒤에서 본 선 그림이라 왼팔(방패)이 왼쪽이다.
            var panel = Group("Robot status", root, new Vector2(-520f, -335f));
            statusPanel = panel.gameObject;
            Box("Panel back", panel, Vector2.zero, new Vector2(170f, 200f), backColor);
            var border = new List<Image>();
            Frame(border, "Panel", panel, Vector2.zero, new Vector2(170f, 200f), 2f);
            foreach (var line in border) line.color = new Color(whiteColor.r, whiteColor.g, whiteColor.b, 0.35f);
            for (int i = 0; i < partLines.Length; i++) partLines[i] = new List<Image>();
            void Piece(Part part, Vector2 center, Vector2 size, bool flashHere = true)
            {
                if (flashHere && partFlash[(int)part] == null)
                {
                    partFlash[(int)part] = Box(part + " flash", panel, center, size, ghostColor);
                    partFlash[(int)part].enabled = false;
                }
                Frame(partLines[(int)part], part.ToString(), panel, center, size, 3f);
            }
            Piece(Part.Head, new Vector2(0f, 72f), new Vector2(24f, 22f));
            Piece(Part.Body, new Vector2(0f, 30f), new Vector2(46f, 52f));
            Piece(Part.LeftArm, new Vector2(-37f, 30f), new Vector2(14f, 52f));
            int before = partLines[(int)Part.LeftArm].Count;
            Piece(Part.LeftArm, new Vector2(-58f, 22f), new Vector2(12f, 66f), false);   // 방패
            shieldLines.AddRange(partLines[(int)Part.LeftArm].GetRange(before, partLines[(int)Part.LeftArm].Count - before));
            Piece(Part.RightArm, new Vector2(37f, 30f), new Vector2(14f, 52f));
            swordLines.Add(Box("Sword", panel, new Vector2(56f, 48f), new Vector2(4f, 84f), whiteColor));   // 검
            partLines[(int)Part.RightArm].AddRange(swordLines);
            Piece(Part.Legs, new Vector2(-12f, -42f), new Vector2(16f, 76f));
            Piece(Part.Legs, new Vector2(12f, -42f), new Vector2(16f, 76f), false);

            // P2: 조준점. 가운데 점, 둘레 고리(과열), 바깥 고리(스캔), 옆 칸 셋(강한 탄).
            reticle = Group("Reticle", root, Vector2.zero);
            Box("Dot", reticle, Vector2.zero, new Vector2(5f, 5f), whiteColor);
            var heatBack = Ring("Heat back", reticle, 84f, ringSprite, new Color(whiteColor.r, whiteColor.g, whiteColor.b, 0.18f));
            heatBack.fillAmount = 1f;
            heatRing = Ring("Heat", reticle, 84f, ringSprite, whiteColor);
            scanRing = Ring("Scan", reticle, 128f, ringSprite, greenColor);
            shots = new Image[3];
            for (int i = 0; i < shots.Length; i++)
            {
                var at = new Vector2(66f + i * 20f, -38f);
                Box("Shot back " + i, reticle, at, new Vector2(16f, 16f), backColor);
                shots[i] = Box("Shot " + i, reticle, at, new Vector2(12f, 12f), whiteColor);
            }

            // P2: 약점. 마름모와 4초 동안 줄어드는 고리.
            weak = Group("Weak point", root, Vector2.zero);
            weakRing = Ring("Weak ring", weak, 96f, ringSprite, greenColor);
            var diamond = Group("Diamond", weak, Vector2.zero);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var diamondLines = new List<Image>();
            Frame(diamondLines, "Diamond", diamond, Vector2.zero, new Vector2(34f, 34f), 4f);
            foreach (var line in diamondLines) line.color = greenColor;

            subtitle = Label("Subtitle", root, new Vector2(0f, subtitleY), new Vector2(1000f, 44f), font, 26, TextAnchor.MiddleCenter);
            message = Label("Message", root, new Vector2(0f, 150f), new Vector2(1100f, 60f), font, 30, TextAnchor.MiddleCenter);
        }

        Text Label(string name, Transform parent, Vector2 position, Vector2 size, Font font, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.alignment = anchor; text.color = whiteColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        /// <summary>눈에서 본 그 방향·자리가 화면 판 위에 놓이는 곳. 판 뒤쪽이면 false.</summary>
        bool OnCanvas(Vector3 direction, out Vector2 at)
        {
            at = Vector2.zero;
            if (head == null || direction.sqrMagnitude < 1e-6f) return false;
            var plane = new Plane(canvas.forward, canvas.position);
            var ray = new Ray(head.position, direction);
            if (!plane.Raycast(ray, out float along)) return false;
            var local = canvas.InverseTransformPoint(ray.GetPoint(along));
            at = new Vector2(local.x, local.y);
            return true;
        }

        static void Active(GameObject go, bool on) { if (go.activeSelf != on) go.SetActive(on); }

        /// <param name="hide">조절 메뉴가 열려 있거나 밖에서 찍을 때 숨긴다.</param>
        /// <param name="note">가운데에 띄울 한 줄(접속 대기, 재기동, 결과). 없으면 빈 문자열.</param>
        public void Show(SkirmishState st, SkirmishSettings s, SkirmishSeat seat, bool vr, bool hide, string note, float dt)
        {
            if (canvas == null) Build();
            Active(canvas.gameObject, !hide);
            if (hide) return;
            Place(seat, vr, s);
            group.alpha = opacity;
            bool mech = seat == SkirmishSeat.Mech;
            float now = Time.unscaledTime;

            if (st.epoch != epoch)
            {
                epoch = st.epoch; maxHp = 0f; ghost = lastFraction = 1f; ghostWait = 0f; lastHull = st.hull; openMax = 0f;
                for (int i = 0; i < flash.Length; i++) flash[i] = 0f;
                saidGuard = saidOpen = saidDrone = false; subtitleLeft = 0f;
                saidAct = st.act; saidBroken = st.swordBroken; gearLost = st.shieldBroken || st.swordBroken; gearOffIn = 0f;
            }
            Lines(st);

            // 보스 체력. 처음 본 체력을 가득 찬 것으로 삼는다(혼자 할 때는 900에서 시작한다).
            bool boss = st.gruntIndex >= 0;
            Active(bossGroup, boss);
            bool open = boss && st.gruntStep == GruntStep.Stagger && st.gruntTimer > 0f;
            if (boss)
            {
                maxHp = Mathf.Max(maxHp, st.gruntHp);
                float fraction = maxHp > 0f ? Mathf.Clamp01(st.gruntHp / maxHp) : 1f;
                if (fraction < lastFraction - 1e-5f) { ghostWait = ghostHold; bright = brightSeconds; }
                lastFraction = fraction;
                ghostWait -= dt;
                if (ghostWait <= 0f) ghost = Mathf.MoveTowards(ghost, fraction, ghostSpeed * dt);
                ghost = Mathf.Max(ghost, fraction);
                bossFill.fillAmount = fraction;
                bossGhost.fillAmount = ghost;
                // 때릴 틈: 튕겨나 있는 동안 줄어드는 초록 줄.
                openMax = open ? Mathf.Max(openMax, st.gruntTimer) : 0f;
                openFill.enabled = open;
                if (open) { openFill.fillAmount = st.gruntTimer / openMax; bright = brightSeconds; }
                bright -= dt;
                bossAlpha.alpha = Mathf.MoveTowards(bossAlpha.alpha, bright > 0f ? 1f : idleAlpha, dt * 4f);
                // P2의 헬기에서는 아래 가운데가 계기판에 가려서 위 가운데에 둔다.
                ((RectTransform)bossGroup.transform).anchoredPosition = new Vector2(0f, mech ? -bossBarOffset : bossBarOffset);
            }

            // 맞았으면: 맞은 부위를 번쩍이고 화면 표시만 흔든다.
            if (lastHull >= 0f && st.hull < lastHull - 0.01f)
            {
                bool swordHit = boss && (st.gruntStep == GruntStep.Strike || st.gruntStep == GruntStep.Recover);
                var part = !swordHit ? Part.Body : st.gruntAttack == GruntAttack.Slam ? Part.LeftArm : Part.RightArm;
                flash[(int)part] = flashSeconds;
                shakeLeft = 0.25f;
            }
            lastHull = st.hull;
            shakeLeft -= dt;
            root.anchoredPosition = mech && shakeLeft > 0f
                ? new Vector2(Mathf.Sin(now * 71f), Mathf.Sin(now * 53f)) * (shake * shakeLeft / 0.25f)
                : Vector2.zero;

            // P1: 막을 쪽. 보스가 칼을 드는 동안 그쪽 가장자리에 붉은 호.
            // 2막부터는 P2가 불러 줘야 한다(속임 베기). 1막에서만 보여 준다.
            bool telegraph = mech && boss && st.act == 0 && (st.gruntStep == GruntStep.Rise || st.gruntStep == GruntStep.Hold || st.gruntStep == GruntStep.Strike);
            arcLeft.enabled = telegraph && st.gruntAttack == GruntAttack.Slam;
            arcRight.enabled = telegraph && st.gruntAttack == GruntAttack.Swing;

            // P1: 로봇 상태판. 선 색이 내구도다.
            Active(statusPanel, mech);
            if (mech)
            {
                float hull = st.hull / Mathf.Max(1f, s.mechHull);
                var color = hull < dangerBelow ? redColor : hull < warnBelow ? yellowColor : whiteColor;
                if (hull < dangerBelow) color.a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(now * 6f));
                for (int i = 0; i < partLines.Length; i++)
                {
                    foreach (var line in partLines[i]) line.color = color;
                    flash[i] -= dt;
                    partFlash[i].enabled = flash[i] > 0f;
                }
                // 잃은 방패와 검은 붉게 됐다가 꺼진다.
                bool lost = st.shieldBroken || st.swordBroken;
                if (lost && !gearLost) gearOffIn = gearOffSeconds;
                gearLost = lost;
                gearOffIn -= dt;
                foreach (var line in shieldLines) { line.enabled = !st.shieldBroken || gearOffIn > 0f; if (st.shieldBroken) line.color = redColor; }
                foreach (var line in swordLines) { line.enabled = !st.swordBroken || gearOffIn > 0f; if (st.swordBroken) line.color = redColor; }
            }

            // P2: 조준점과 그 둘레.
            Vector2 aimAt = Vector2.zero;
            bool reticleOn = !mech && OnCanvas(Aim, out aimAt);
            Active(reticle.gameObject, reticleOn);
            if (reticleOn)
            {
                reticle.anchoredPosition = aimAt;
                heatRing.fillAmount = Mathf.Clamp01(st.cannonHeat);
                heatRing.color = st.cannonOverheated ? redColor : whiteColor;
                for (int i = 0; i < shots.Length; i++) shots[i].enabled = i < st.missiles;
                bool scanning = boss && st.weakTimer <= 0f && st.lockTarget == SkirmishSession.GruntTarget;
                scanRing.enabled = scanning;
                scanRing.fillAmount = st.lockProgress;
            }

            // P2: 약점 표시.
            bool weakOn = !mech && boss && st.weakTimer > 0f && head != null && OnCanvas(st.weakPoint - head.position, out aimAt);
            Active(weak.gameObject, weakOn);
            if (weakOn)
            {
                weak.anchoredPosition = aimAt;
                weakRing.fillAmount = Mathf.Clamp01(st.weakTimer / Mathf.Max(0.01f, s.weakPointSeconds));
            }

            // 자막: P2의 헬기에서는 아래쪽이 기수에 가려서 보스 체력 줄 위에 둔다.
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, mech ? subtitleY : bossBarOffset + 72f);
            subtitleLeft -= dt;
            subtitle.text = subtitleLeft > 0f ? pendingLine : "";
            message.text = note ?? "";
        }

        /// <summary>모니터에서는 머리에, 헤드셋에서는 자리에(머리를 따라오지 않게) 붙인다. 자리가 바뀌면 다시 놓는다.</summary>
        void Place(SkirmishSeat seat, bool vr, SkirmishSettings s)
        {
            if (placed && placedSeat == seat && placedVr == vr && canvas.parent != null) return;
            if (head == null) return;
            if (!vr || rig == null)
            {
                canvas.SetParent(head, false);
                canvas.localPosition = new Vector3(0f, 0f, 1f);
                canvas.localRotation = Quaternion.identity;
                canvas.localScale = Vector3.one * 0.001f;
            }
            else
            {
                // 자리의 정면(로봇 방향 또는 P2가 보는 방향) 눈앞에 세워 두고 XR Origin에 붙인다.
                var facing = Quaternion.Euler(0f, seat == SkirmishSeat.Mech ? s.mechYaw : s.gunnerYaw, 0f);
                canvas.SetParent(rig, true);
                canvas.SetPositionAndRotation(head.position + facing * Vector3.forward * distance, facing);
                var scale = Vector3.one * (0.001f * distance);
                var parentScale = rig.lossyScale;
                canvas.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            }
            placed = true; placedSeat = seat; placedVr = vr;
        }

        /// <summary>운영자가 착용자 정면을 다시 맞췄을 때: 다음에 다시 놓는다.</summary>
        public void Replace() => placed = false;
    }
}
