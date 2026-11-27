using UnityEngine;
using UnityEngine.UI;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 보스전 화면 표시: 얇고 긴 줄 몇 개만 있는 간결한 모습(선우 2026-11-27). 숫자는 쓰지 않는다.
    /// 보스 체력은 시야 아래 가운데의 긴 붉은 줄(두 사람 모두), P1은 왼쪽 위에 내구도 줄과 고로 칸 셋,
    /// P2는 왼쪽 위에 과열 줄과 고로 칸 셋, 왼쪽 아래에 강한 탄 칸 셋, 스캔 중에는 보스 둘레에 차오르는 고리.
    /// 화면은 시작할 때 코드로 만든다. 헤드셋에서는 머리가 아니라 자리에 고정한다(눈앞 distance).
    /// </summary>
    public class BossFightHud : MonoBehaviour
    {
        [SerializeField, Tooltip("XR Origin 루트(헤드셋일 때 여기에 붙여 자리에 고정한다).")] Transform rig;
        [SerializeField, Tooltip("머리(모니터 확인일 때는 머리에 붙인다).")] Transform head;
        [SerializeField, Tooltip("한글이 나오는 OS 글꼴(앞에서부터 있는 것).")] string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };
        [SerializeField, Min(0.5f), Tooltip("헤드셋: 눈앞 이 거리에 띄운다(m).")] float distance = 2.5f;
        [SerializeField, Range(0.2f, 1f), Tooltip("전체 비침(1 = 불투명).")] float opacity = 0.85f;
        [SerializeField, Min(0f), Tooltip("깎인 만큼이 밝게 남아 있는 시간(초).")] float ghostHold = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("남아 있던 부분이 줄어드는 빠르기(1초에 줄 전체의 몇 배).")] float ghostSpeed = 1.2f;
        [SerializeField, Tooltip("P2 자리에서 보스 체력 줄을 위로 올리는 양(캔버스 칸). 0이면 P1과 같은 아래 가운데.")] float gunnerBossRaise = 760f;
        [SerializeField] Color bossColor = new Color(0.78f, 0.1f, 0.08f);
        [SerializeField] Color ghostColor = new Color(1f, 0.82f, 0.6f);
        [SerializeField] Color hullColor = new Color(0.8f, 0.14f, 0.1f);
        [SerializeField] Color heatColor = new Color(0.95f, 0.6f, 0.15f);
        [SerializeField] Color lineColor = new Color(0.92f, 0.9f, 0.84f);
        [SerializeField] Color backColor = new Color(0f, 0f, 0f, 0.55f);

        // 캔버스는 1600×900 칸. 1m 앞에서 가로 1.6m(시야 약 77°)로 보이게 줄여 쓴다.
        const float Width = 1600f, Height = 900f, BossWidth = 900f;

        RectTransform canvas;
        CanvasGroup group;
        Image bossFill, bossGhost, sideFill, ring;
        Image[] furnace, shots;
        GameObject bossGroup, sideGroup, shotGroup;
        Text label, message;
        Sprite ringSprite;
        float ghost = 1f, ghostWait, maxHp, lastFraction = 1f;
        int epoch = -1;
        SkirmishSeat placedSeat;
        bool placed, placedVr;

        public void Bind(Transform rigRoot, Transform headTransform) { rig = rigRoot; head = headTransform; }

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

        /// <summary>왼쪽 끝에서 차오르는 줄. 뒷판, (있으면) 밝은 잔상, 채움 순서로 겹친다.</summary>
        Image Bar(string name, Transform parent, Vector2 leftCenter, Vector2 size, Color color, out Image ghostImage, bool withGhost)
        {
            var center = leftCenter + new Vector2(size.x * 0.5f, 0f);
            Box(name + " back", parent, center, size + new Vector2(4f, 4f), backColor);
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

        Sprite white;

        static Sprite MakeRing(int size, float inner, float outer)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Scan ring", hideFlags = HideFlags.DontSave };
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
            ringSprite = MakeRing(256, 0.86f, 0.98f);
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, 28);

            // 보스 체력: 아래 가운데의 길고 얇은 줄, 800과 400 자리에 가는 눈금, 줄 위 왼쪽에 작은 글씨.
            bossGroup = new GameObject("Boss").AddComponent<RectTransform>().gameObject;
            bossGroup.transform.SetParent(canvas, false);
            var bossLeft = new Vector2(-BossWidth * 0.5f, -340f);
            bossFill = Bar("Boss hp", bossGroup.transform, bossLeft, new Vector2(BossWidth, 9f), bossColor, out bossGhost, true);
            foreach (float mark in new[] { 1f / 3f, 2f / 3f })
                Box("Act mark", bossGroup.transform, bossLeft + new Vector2(BossWidth * mark, 0f), new Vector2(2f, 21f), lineColor);
            label = Label("Boss name", bossGroup.transform, bossLeft + new Vector2(120f, 24f), new Vector2(240f, 34f), font, 22, TextAnchor.MiddleLeft);
            label.text = "적 기체";

            // 왼쪽 위: 줄 하나(P1 = 로봇 내구도, P2 = 과열)와 고로 칸 셋.
            sideGroup = new GameObject("Side").AddComponent<RectTransform>().gameObject;
            sideGroup.transform.SetParent(canvas, false);
            var sideLeft = new Vector2(-720f, 370f);
            sideFill = Bar("Side bar", sideGroup.transform, sideLeft, new Vector2(330f, 8f), hullColor, out _, false);
            furnace = new Image[3];
            for (int i = 0; i < furnace.Length; i++)
                furnace[i] = Box("Furnace " + i, sideGroup.transform, sideLeft + new Vector2(8f + i * 24f, -24f), new Vector2(15f, 15f), lineColor);

            // P2 왼쪽 아래: 강한 탄 칸 셋.
            shotGroup = new GameObject("Shots").AddComponent<RectTransform>().gameObject;
            shotGroup.transform.SetParent(canvas, false);
            shots = new Image[3];
            for (int i = 0; i < shots.Length; i++)
            {
                var at = new Vector2(-708f + i * 36f, -338f);
                Box("Shot back " + i, shotGroup.transform, at, new Vector2(28f, 28f), backColor);
                shots[i] = Box("Shot " + i, shotGroup.transform, at, new Vector2(22f, 22f), lineColor);
            }

            // P2 스캔: 보스 둘레에 2초 동안 차오르는 고리.
            ring = Box("Scan ring", canvas, Vector2.zero, new Vector2(230f, 230f), lineColor);
            ring.sprite = ringSprite;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;

            message = Label("Message", canvas, new Vector2(0f, 150f), new Vector2(1100f, 60f), font, 30, TextAnchor.MiddleCenter);
        }

        Text Label(string name, Transform parent, Vector2 position, Vector2 size, Font font, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.alignment = anchor; text.color = lineColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        /// <param name="hide">조절 메뉴가 열려 있거나 밖에서 찍을 때 숨긴다.</param>
        /// <param name="note">가운데에 띄울 한 줄(접속 대기, 재기동, 결과). 없으면 빈 문자열.</param>
        public void Show(SkirmishState st, SkirmishSettings s, SkirmishSeat seat, bool vr, bool hide, string note, float dt)
        {
            if (canvas == null) Build();
            if (canvas.gameObject.activeSelf == hide) canvas.gameObject.SetActive(!hide);
            if (hide) return;
            Place(seat, vr, s);
            group.alpha = opacity;

            // 보스 체력. 처음 본 체력을 가득 찬 것으로 삼는다(혼자 할 때는 900에서 시작한다).
            if (st.epoch != epoch) { epoch = st.epoch; maxHp = 0f; ghost = lastFraction = 1f; ghostWait = 0f; }
            bool boss = st.gruntIndex >= 0;
            if (bossGroup.activeSelf != boss) bossGroup.SetActive(boss);
            if (boss)
            {
                maxHp = Mathf.Max(maxHp, st.gruntHp);
                float fraction = maxHp > 0f ? Mathf.Clamp01(st.gruntHp / maxHp) : 1f;
                if (fraction < lastFraction - 1e-5f) ghostWait = ghostHold;
                lastFraction = fraction;
                ghostWait -= dt;
                if (ghostWait <= 0f) ghost = Mathf.MoveTowards(ghost, fraction, ghostSpeed * dt);
                ghost = Mathf.Max(ghost, fraction);
                bossFill.fillAmount = fraction;
                bossGhost.fillAmount = ghost;
            }

            bool mech = seat == SkirmishSeat.Mech;
            // P2의 헬기에서는 아래 가운데가 계기판에 가려서, 보스 체력 줄을 위 가운데로 올린다.
            ((RectTransform)bossGroup.transform).anchoredPosition = new Vector2(0f, mech ? 0f : gunnerBossRaise);
            if (mech)
            {
                float hull = Mathf.Clamp01(st.hull / Mathf.Max(1f, s.mechHull));
                sideFill.fillAmount = hull;
                // 위태로우면 깜빡인다.
                bool low = s.lowHullBelow > 0f && st.hull < s.lowHullBelow;
                var color = hullColor;
                if (low) color.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                sideFill.color = color;
            }
            else
            {
                sideFill.fillAmount = Mathf.Clamp01(st.cannonHeat);
                sideFill.color = st.cannonOverheated ? hullColor : heatColor;
            }
            for (int i = 0; i < furnace.Length; i++) furnace[i].enabled = i < st.furnace;
            if (shotGroup.activeSelf == mech) shotGroup.SetActive(!mech);
            for (int i = 0; i < shots.Length; i++) shots[i].enabled = i < st.missiles;

            // 스캔 고리: 보스 가운데가 화면 판 위에 놓이는 자리에 그린다.
            bool scanning = !mech && boss && (st.weakTimer > 0f || st.lockTarget == SkirmishSession.GruntTarget);
            if (ring.gameObject.activeSelf != scanning) ring.gameObject.SetActive(scanning);
            if (scanning && head != null)
            {
                var target = st.weakTimer > 0f ? st.weakPoint : SkirmishSession.GruntCenter(st, s);
                var plane = new Plane(canvas.forward, canvas.position);
                var ray = new Ray(head.position, target - head.position);
                if (plane.Raycast(ray, out float along))
                {
                    var local = canvas.InverseTransformPoint(ray.GetPoint(along));
                    ((RectTransform)ring.transform).anchoredPosition = new Vector2(local.x, local.y);
                }
                ring.fillAmount = st.weakTimer > 0f ? 1f : st.lockProgress;
                ring.color = st.weakTimer > 0f ? heatColor : lineColor;
                float size = st.weakTimer > 0f ? 90f : 230f;
                ((RectTransform)ring.transform).sizeDelta = new Vector2(size, size);
            }
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
