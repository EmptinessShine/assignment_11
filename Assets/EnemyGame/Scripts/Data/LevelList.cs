using UnityEngine;

// all levels in order. the menu and the Game scene use the same list.
[CreateAssetMenu(menuName = "Enemy Game/Level List")]
public class LevelList : ScriptableObject
{
    public LevelData[] levels;
}
