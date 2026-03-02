using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ChessSurvivor/Upgrade Catalog", fileName = "UpgradeCatalog")]
public class UpgradeCatalog : ScriptableObject
{
    public List<PieceStatUpgrade> pieceUpgrades = new();
    public List<PlayerStatUpgrade> playerUpgrades = new();
}

[Serializable]
public class PieceStatUpgrade
{
    public PieceType pieceType;
    public string id;
    public string displayName;
    public int summonChargeDelta;
    public int hpDelta;
    public int damageDelta;
}

[Serializable]
public class PlayerStatUpgrade
{
    public string id;
    public string displayName;
    public int maxHpDelta;
    public int moveRangeDelta;
    public int xpGainDelta;
}
