using System.IO;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// Art/BossMecha/Motions에 넣은 동작 파일(Mixamo의 "FBX for Unity", 캐릭터 없이 동작만)을 사람형으로 들여온다.
    /// 동작 이름을 파일 이름으로 바꾸고(Mixamo 것은 전부 "mixamo.com"이다), 서 있기·걷기처럼 도는 동작은 반복으로 둔다.
    /// 제자리에서 움직이게 한다: 위치는 규칙(SkirmishSession)이 정하므로 동작의 앞뒤 이동은 쓰지 않는다. 몸을 트는 것과 주저앉는 것은 자세에 남긴다.
    /// 이 폴더는 git에 없다(받은 사람이 같은 자리에 풀면 이 설정으로 똑같이 들어온다).
    /// </summary>
    public class BossMotionImport : AssetPostprocessor
    {
        public const string Folder = "Assets/_Anseong/Art/BossMecha/Motions/";
        static readonly string[] Looping = { "idle", "walk", "run", "strafe" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = false;
            foreach (var word in Looping) loop |= name.ToLowerInvariant().Contains(word);
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = clips.Length == 1 ? name : name + "_" + i;
                clips[i].loopTime = loop;
                // 몸의 방향과 높이는 자세에 넣는다(루트를 움직이지 않는다).
                clips[i].lockRootRotation = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].lockRootHeightY = true;
                clips[i].keepOriginalPositionY = true;
                // 앞뒤·좌우 이동은 자세에 넣지 않는다. 보스의 Animator는 루트 이동을 쓰지 않으므로 제자리에서 움직인다.
                clips[i].lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
        }
    }
}
