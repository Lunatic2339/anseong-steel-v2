using System.IO;
using UnityEngine;

namespace Anseong.Core
{
    /// <summary>
    /// 개발용 기록(펀치·가드 CSV, 세션 로그, 조절값)을 두는 곳. 에디터에서는 프로젝트 폴더의 Logs/Anseong(git 무시 폴더)이라
    /// Claude가 선우 PC 프로젝트 폴더에서 바로 읽을 수 있다(선우 10/28). 빌드에서는 persistentDataPath.
    /// </summary>
    public static class LogFolder
    {
        public static string Root
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Anseong"));
#else
                return Application.persistentDataPath;
#endif
            }
        }
    }
}
