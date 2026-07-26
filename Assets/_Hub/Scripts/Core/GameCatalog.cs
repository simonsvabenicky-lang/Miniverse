using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Miniverse.Hub
{
    /// <summary>
    /// Finds every graduated minigame by scanning Resources for MiniGameDef assets, instead
    /// of reading a hand-maintained list. See MiniGameDef for why: it keeps each game's
    /// graduation a self-contained add, never a shared-file edit.
    /// </summary>
    public static class GameCatalog
    {
        const string ResourcesFolder = "GameCatalog";

        static List<MiniGameDef> _cache;

        public static IReadOnlyList<MiniGameDef> All
        {
            get
            {
                if (_cache == null)
                {
                    _cache = Resources.LoadAll<MiniGameDef>(ResourcesFolder)
                        .Where(def => def.enabled)
                        .OrderBy(def => def.displayName)
                        .ToList();
                }
                return _cache;
            }
        }

        /// <summary>Editor tooling calls this after adding/removing a MiniGameDef so a stale cache in a running session doesn't hide it.</summary>
        public static void Invalidate() => _cache = null;
    }
}
