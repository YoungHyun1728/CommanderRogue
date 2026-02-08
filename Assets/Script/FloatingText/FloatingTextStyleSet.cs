using UnityEngine;

[CreateAssetMenu(menuName="Game/UI/Floating Text Style Set")]
public class FloatingTextStyleSet : ScriptableObject
{
    public FloatingTextStyle damage;   // 빨강
    public FloatingTextStyle heal;     // 초록
    public FloatingTextStyle status;   // 둔화/발화/기절
    public FloatingTextStyle speech;   // 생성 대사/연출
}