using System;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 섹터(레벨) 목록과 순서. Step 27 (Build Levels) 이 Levels/*.txt 의 @name/@subtitle 로 채운다.
    /// 타이틀의 섹터 선택은 씬을 열지 않고 이 목록만 보고 그린다. Resources/LevelCatalog 에 하나만 둔다.
    /// </summary>
    public sealed class LevelCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string scene;
            [Tooltip("예: SECTOR 01")]
            public string code;
            [Tooltip("예: 네온 옥상")]
            public string title;
            [Tooltip("이 섹터의 데이터 칩 개수")]
            public int chips;
        }

        public Entry[] levels = Array.Empty<Entry>();

        static LevelCatalog instance;

        public static LevelCatalog Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<LevelCatalog>("LevelCatalog");
                return instance;
            }
        }

        public int IndexOf(string sceneName) => Array.FindIndex(levels, e => e.scene == sceneName);
    }
}
