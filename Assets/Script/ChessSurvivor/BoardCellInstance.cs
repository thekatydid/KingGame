using UnityEngine;

public class BoardCellInstance : MonoBehaviour
{
    [SerializeField] private Renderer tileRenderer;
    [SerializeField] private Material lightTileMaterial;
    [SerializeField] private Material darkTileMaterial;
    [SerializeField] private bool useDarkTile;

    public void SetUseDarkTile(bool useDark)
    {
        useDarkTile = useDark;
        ApplyTileMaterial();
    }

    private void Awake()
    {
        ApplyTileMaterial();
    }

    private void OnValidate()
    {
        ApplyTileMaterial();
    }

    private void ApplyTileMaterial()
    {
        if (tileRenderer == null)
        {
            tileRenderer = transform.Find("Tile")?.GetComponent<Renderer>();
        }

        if (tileRenderer == null)
        {
            return;
        }

        Material target = useDarkTile ? darkTileMaterial : lightTileMaterial;
        if (target != null)
        {
            tileRenderer.sharedMaterial = target;
        }
    }
}
