using UnityEngine;

// BGM용 ID 모음 (타이틀, 이벤트, 바이옴, 보스 등)
public enum BgmId
{
    Title,
    Event_Generic,
    Event_BossIntro,
    Event_NecromancerIntro,

    Biome_Forest,
    Biome_Plains,
    Biome_DeepForest,
    Biome_Cave,
    Biome_Lake,
    Biome_Snow,
    Biome_Desert,
    Biome_Labyrinth,

    BossFight_Generic,
    BossFight_Necromancer,
    Game_Clear,
    Game_Over
}

// SFX/보이스 공용 ID (필요시 자유롭게 추가)
public enum SfxId
{
    Ui_Click,
    Ui_Confirm,
    Ui_Cancel,
    Footstep,
    Hit_Light,
    Hit_Heavy,
    Skill_Cast,
    Explosion,
    Pickup,
    Enemy_Die,
    Boss_Roar,
    Necromancer_Laugh
}

// 대사/보이스 전용 ID
public enum VoiceId
{
    Boss_Dialogue,
    Necromancer_Dialogue,
    Ally_Dialogue
}
