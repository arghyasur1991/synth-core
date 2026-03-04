namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Superset of Unity's HumanBodyBones covering all bones relevant to physics
    /// simulation. Includes standard humanoid bones plus extended bones (pectorals,
    /// gluteals) that Unity's Avatar doesn't map but Synth uses for soft-body physics.
    /// </summary>
    public enum SynthBone
    {
        // --- Center chain ---
        Hips = 0,
        Spine = 1,
        Chest = 2,
        UpperChest = 3,
        Neck = 4,
        Head = 5,
        Jaw = 6,

        // --- Left arm chain ---
        LeftShoulder = 10,
        LeftUpperArm = 11,
        LeftLowerArm = 12,
        LeftHand = 13,

        // --- Right arm chain ---
        RightShoulder = 20,
        RightUpperArm = 21,
        RightLowerArm = 22,
        RightHand = 23,

        // --- Left leg chain ---
        LeftUpperLeg = 30,
        LeftLowerLeg = 31,
        LeftFoot = 32,
        LeftToes = 33,

        // --- Right leg chain ---
        RightUpperLeg = 40,
        RightLowerLeg = 41,
        RightFoot = 42,
        RightToes = 43,

        // --- Eyes ---
        LeftEye = 50,
        RightEye = 51,

        // --- Extended bones (not in standard Unity Avatar) ---
        LeftPectoral = 60,
        RightPectoral = 61,
        LeftGluteal = 62,
        RightGluteal = 63,

        Unknown = -1
    }

    public enum BoneSide
    {
        Center,
        Left,
        Right
    }
}
