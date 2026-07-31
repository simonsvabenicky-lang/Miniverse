namespace FlowSort.Blocks
{
    /// <summary>
    /// Every number that defines how the game feels, in one place. World-space positions are NOT
    /// here — those are solved per-device at runtime by Layout, because the play frame has to fit
    /// the screen width whatever the aspect ratio.
    /// </summary>
    public static class BlockTuning
    {
        // --- Grid ---
        public const float TileSize = 1f;

        /// <summary>
        /// Columns and rows. Fewer, chunkier blocks: the frame furniture around the grid is a
        /// fixed world size, so shrinking the grid is what actually makes a block bigger on
        /// screen — raising TileSize just widens the camera by the same factor and nets nothing.
        /// Pictures are authored in a fixed design space and scaled to whatever this is, so these
        /// can move without redrawing anything (see PictureAuthor.Raster).
        /// </summary>
        public const int WallWidth = 17;
        public const int WallHeight = 20;
        public const int ChunkRows = 8;

        // --- Level progression ---

        /// <summary>
        /// Fraction of the picture's blocks that actually appear on level 1, ramping to the whole
        /// thing by FullFillLevel. Early levels should be short; a first level that takes as long
        /// as a tenth one is just a slow tutorial.
        ///
        /// It does not go much below this: the board is meant to read as mostly filled, and at
        /// 0.5 level 1 came out looking like the sparse scattered boards this replaced.
        /// </summary>
        public const float StartFill = 0.75f;
        public const int FullFillLevel = 5;

        /// <summary>
        /// Shifts the whole curve up, so level 1 plays like the old level 3. The opening levels
        /// were soft enough that tapping more or less at random still cleared them.
        /// </summary>
        public const int DifficultyOffset = 2;

        /// <summary>The level the fill and line ordering are actually computed from.</summary>
        public static int DifficultyLevel(int level) => level + DifficultyOffset;

        /// <summary>
        /// How much of the picture appears. Levels get longer as they go, rather than opening
        /// with one as long as any later one. Shared with the balance simulator.
        /// </summary>
        public static float FillForLevel(int level) => UnityEngine.Mathf.Lerp(
            StartFill, 1f,
            UnityEngine.Mathf.Clamp01((DifficultyLevel(level) - 1) / (float)FullFillLevel));

        /// <summary>
        /// Levels over which line ordering swings from helpful to hostile. See TowerSlots.Fill —
        /// early on the colours you can shoot right now are at the front of the lines, later the
        /// ones you need are buried behind the ones you don't.
        /// </summary>
        public const int DifficultyRampLevels = 14;

        /// <summary>
        /// Ceiling on how hostile the line ordering gets. See TowerSlots.OrderByDifficulty — at
        /// 1.0 the hardest levels invert completely and the simulated win rate collapses to
        /// single digits, which reads as unfair rather than difficult.
        /// </summary>
        public const float MaxHostility = 0.55f;

        /// <summary>
        /// Visible gap between blocks, as a fraction of TileSize. The sprite faces already carry
        /// their own black outline, so this only has to open enough violet between neighbours for
        /// each square to read as a separate tile.
        /// </summary>
        public const float BlockGap = 0.10f;

        // --- Camera ---
        public const float CameraFov = 30f;
        public const float CameraDistance = 103f;

        // --- Shots ---
        public const float BallSpeed = 52f;
        public const float BallRadius = 0.3f;
        public const float MaxStepDistance = 0.25f;
        public const int MaxBalls = 512;
        public const float BallTrailLength = 1.5f;

        // --- Towers ---

        /// <summary>
        /// Landing squares. Filling all of these with returned towers loses the level. The count
        /// is owned by the player and bought with coins — see PlayerProfile.LandingSlots — so the
        /// live value comes from Layout.SlotCount, not from here.
        /// </summary>
        public const float SlotSpacing = 4.9f;

        /// <summary>Queue lines. Only the front tower of each can be sent; more lines = more choice.</summary>
        public const int LineCount = 3;

        /// <summary>How many of each line fit on screen; deeper ones are held back until they advance.</summary>
        public const int VisibleLineDepth = 3;
        public const float LineSpacingY = 4.6f;

        /// <summary>
        /// Towers allowed on the belt at once.
        ///
        /// Without a cap you could empty every line onto the track in one go and let the lap sort
        /// it out, which removed the decision the lines exist for and made levels trivial. Five is
        /// enough to keep the track busy and still force you to choose what goes next.
        /// </summary>
        public const int MaxOnBelt = 5;

        /// <summary>How fast a deployed tower travels around the belt, in world units per second.</summary>
        public const float ConveyorSpeed = 21f;

        /// <summary>
        /// Seconds between shots. Kept under one tile of travel at ConveyorSpeed, so a tower
        /// cannot skid past a column of its own colour without getting a shot into it.
        /// </summary>
        public const float FireInterval = 0.04f;

        /// <summary>
        /// How far a tower must travel between shots, in tiles.
        ///
        /// This is what stops a tower parking on one column and drilling it to the back wall. One
        /// shot per column per pass means a tower almost never empties in a single lap: it spends
        /// what the exposed face of the picture will take, then goes and waits in a landing square
        /// until some other colour has opened a way through for the rest of its ammo.
        /// </summary>
        public const float TilesBetweenShots = 1f;

        public const float RecoilKick = 0.5f;
        public const float RecoilSpring = 90f;
        public const float RecoilDamping = 12f;
        public const float TurretPopTime = 0.14f;

        // --- Ammo economy (this is the difficulty dial) ---

        public const int AmmoMin = 10;
        public const int AmmoMax = 40;
        public const int AmmoStep = 5;

        /// <summary>
        /// Rounds handed out per block on the board, per colour. Above 1.0 the level is always
        /// winnable on paper — there is strictly more ammo of each colour than there are blocks of
        /// it — so losing is only ever the result of clogging the landing squares, never of being
        /// dealt an impossible hand. See TowerSlots.Fill.
        ///
        /// Ramped, because it is the only difficulty lever that does not run out. Fill hits 100%
        /// by level 4 and line hostility caps a few levels later, so without this the game stops
        /// getting harder and every level from then on plays identically — which is its own way
        /// of losing a player's attention.
        /// </summary>
        public static float WinAmmoMargin(int level) => UnityEngine.Mathf.Lerp(
            1.34f, 1.06f,
            UnityEngine.Mathf.Clamp01((DifficultyLevel(level) - 1) / 15f));

        // --- Score ---
        public const int ScorePerBlock = 10;
        public const int LevelClearKeys = 5;

        // --- Helpers ---

        /// <summary>Local-space X of a cell centre; the wall's pivot is its bottom-left corner.</summary>
        public static float CellLocalX(int x) => (x + 0.5f) * TileSize;

        /// <summary>Local-space Y of a cell centre. y = 0 is the wall's bottom row.</summary>
        public static float CellLocalY(int y) => (y + 0.5f) * TileSize;
    }
}
