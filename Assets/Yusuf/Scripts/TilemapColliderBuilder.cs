using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic; // Dictionary için gerekli

public class TilemapColliderBuilder : MonoBehaviour
{
    // --- 1. Shape Struct'ı (GameStart.cs'ten alındı) ---
    private struct Shape 
    { 
        public float ox, oy, sx, sy; 
        public Shape(float ox, float oy, float sx, float sy) 
        { 
            this.ox = ox; this.oy = oy; this.sx = sx; this.sy = sy; 
        } 
    }

    // --- 2. WALL_SHAPES Kütüphanesi (GameStart.cs'ten alındı) ---
    private static readonly Dictionary<int, Shape> WALL_SHAPES = CreateWallShapeMap();

    private static Dictionary<int, Shape> CreateWallShapeMap()
    {
        //[cite_start]// (Gemini) Bu bölüm doğrudan GameStart.cs'ten kopyalandı 
        var d = new Dictionary<int, Shape>(32);

        // helper local
        void Add(int[] ids, float ox, float oy, float sx, float sy)
        { var s = new Shape(ox, oy, sx, sy); for (int k = 0; k < ids.Length; k++) d[ids[k]] = s; }

        // 26,29,32,58 → top-left corner (0.3125 x 0.3125) at (+0.344, -0.344)
        Add(new[] { 26, 29, 32, 58 }, +0.3440f, -0.3440f, 0.3125f, 0.3125f);

        // 27,30,33,59 → top edge (1.0 x 0.3125) at (0, -0.344)
        Add(new[] { 27, 30, 33, 59 }, 0f, -0.3440f, 1f, 0.3125f);

        // 39,41,44,66 → left edge (0.3125 x 1.0) at (+0.344, 0)
        Add(new[] { 39, 41, 44, 66 }, +0.3440f, 0f, 0.3125f, 1f);

        // 40,43,46,67 → right edge (0.3125 x 1.0) at (-0.344, 0)
        Add(new[] { 40, 43, 46, 67 }, -0.3440f, 0f, 0.3125f, 1f);

        // 49,52,55,73 → bottom-left corner at (+0.344, +0.344)
        Add(new[] { 49, 52, 55, 73 }, +0.3440f, +0.3440f, 0.3125f, 0.3125f);

        // 50,53,56,74 → bottom edge at (0, +0.344)
        Add(new[] { 50, 53, 56, 74 }, 0f, +0.3440f, 1f, 0.3125f);

        // 51,54,57,75 → bottom-right corner at (-0.344, +0.344)
        Add(new[] { 51, 54, 57, 75 }, -0.3440f, +0.3440f, 0.3125f, 0.3125f);

        d.Add(0, new Shape(0f, 0f, 1f, 1f));

        d.Add(1, new Shape(0f, 0f, 1f, 1f));

        return d;
    }

    // --- 3. INSPECTOR AYARLARI ---
    [Header("Hierarchy")]
    [Tooltip("Tüm Tilemap'leri içeren Grid objesi")]
    [SerializeField] private Transform gridRoot;

    [Header("Tilemap Names")]
    [Tooltip("Duvarların olduğu Tilemap objesinin adı")]
    [SerializeField] private string wallMapName = "Tilemap_Wall";
    [Tooltip("Mobilyaların olduğu Tilemap objesinin adı")]
    [SerializeField] private string furnitureMapName = "Tilemap_Furniture";
    [Tooltip("Zeminin olduğu Tilemap objesinin adı")]
    [SerializeField] private string floorMapName = "Tilemap_Floor";

    [Header("Layer Settings")]
    [Tooltip("Duvar collider'larının atanacağı Layer")]
    [SerializeField] private LayerMask wallLayer;
    [Tooltip("Mobilya collider'larının atanacağı Layer")]
    [SerializeField] private LayerMask furnitureLayer;
    [Tooltip("Zemin objesinin atanacağı Layer")]
    [SerializeField] private LayerMask floorLayer;

    [Header("Collider & Fallbacks")]
    [Tooltip("Oluşturulan 3D BoxCollider'ların Z eksenindeki derinliği")]
    [SerializeField] private float colliderDepth = 1f;
    [Tooltip("Sprite index'i bilinmiyorsa tam 1x1 collider oluştur")]
    [SerializeField] private bool fallbackToFullCell = true;
    [Tooltip("Oluşturulan objeler 'Static' olarak işaretlensin mi?")]
    [SerializeField] private bool markStatic = true;


    // --- 4. SAĞ TIK MENÜSÜ ---
    [ContextMenu("Generate Colliders")]
    private void GenerateColliders()
    {
        Debug.Log("Collider oluşturma işlemi başlıyor...");
        
        // Önce mevcut collider'ları temizle
        ClearGeneratedColliders();

        BuildColliders();
        
        Debug.Log("Collider oluşturma tamamlandı!");
    }
    
    [ContextMenu("Clear Generated Colliders")]
    private void ClearGeneratedColliders()
    {
        List<Transform> childrenToDestroy = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("WallCol_") || child.name.StartsWith("FurnCol_"))
            {
                childrenToDestroy.Add(child);
            }
        }

        Debug.Log($"{childrenToDestroy.Count} adet eski collider temizleniyor...");
        foreach (var child in childrenToDestroy)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }


    // --- 5. SAĞLADIĞINIZ METODLAR (Uyarlanmış) ---
    // (Gemini) 'wallLayer' gibi değişkenleri LayerMask'tan int'e (layer index) çevirir.
    
    private void BuildColliders()
    {
        if (!gridRoot)
        {
            Debug.LogError("Grid Root atanmamış!", this);
            return;
        }

        foreach (Transform child in gridRoot)
        {
            if (!child) continue;
            var tilemap = child.GetComponent<Tilemap>();
            if (!tilemap) continue;

            if (child.name == wallMapName)
                BuildWallColliders(tilemap);
            else if (child.name == furnitureMapName)
                BuildFurnitureColliders(tilemap);
            else if (child.name == floorMapName)
                // LayerMask'ı int'e çevir (örn: 00010000 -> 4)
                child.gameObject.layer = LayerMaskToLayerIndex(floorLayer);
        }
    }

    private void BuildWallColliders(Tilemap tilemap)
    {
        var bounds = tilemap.cellBounds;
        var parent = transform; 

        var grid = tilemap.layoutGrid;
        float sx = grid ? Mathf.Abs(grid.cellSize.x) : 1f;
        float sy = grid ? Mathf.Abs(grid.cellSize.y) : 1f;
        
        int layer = LayerMaskToLayerIndex(wallLayer);
        if (layer == -1) Debug.LogWarning("Wall Layer atanmamış, 'Default' kullanılacak.", this);

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            var pos = new Vector3Int(x, y, 0);

            var baseTile = tilemap.GetTile(pos);
            if (!baseTile) continue;

            Sprite sprite = (baseTile is Tile t && t.sprite) ? t.sprite : tilemap.GetSprite(pos);
            if (!sprite) continue;

            //[cite_start]// (Gemini) Kodun bu kısmı GameStart.cs'ten alındı [cite: 104-115]
            int index = -1;
            var name = sprite.name;
            int k = name.LastIndexOf("tilemap_");
            if (k >= 0)
            {
                int start = k + 8; // "tilemap_" (8 karakter)
                if (start < name.Length)
                    int.TryParse(name.Substring(start), out index);
            }

            //[cite_start]// (Gemini) Kodun bu kısmı GameStart.cs'ten alındı [cite: 117-124]
            Shape shape = new Shape();
            bool hasShape = index >= 0 && WALL_SHAPES.TryGetValue(index, out shape);
            if (!hasShape)
            {
                if (!fallbackToFullCell) continue;
                shape = new Shape(0f, 0f, 1f, 1f); // Fallback: 1x1 tam kare
            }

            var cellWorldPos = tilemap.GetCellCenterWorld(pos);
            var go = new GameObject($"WallCol_{x}_{y}", typeof(BoxCollider));
            go.layer = layer;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = cellWorldPos;

            var col = go.GetComponent<BoxCollider>();
            col.center = new Vector3(shape.ox * sx, shape.oy * sy, 0f);
            col.size = new Vector3(shape.sx * sx, shape.sy * sy, colliderDepth);

            if (markStatic) go.isStatic = true;
        }
    }

    private void BuildFurnitureColliders(Tilemap tilemap)
    {
        var bounds = tilemap.cellBounds;
        var parent = transform; 

        int layer = LayerMaskToLayerIndex(furnitureLayer);
        if (layer == -1) Debug.LogWarning("Furniture Layer atanmamış, 'Default' kullanılacak.", this);

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            var pos = new Vector3Int(x, y, 0);
            var baseTile = tilemap.GetTile(pos);
            if (!baseTile) continue;

            Sprite sprite = (baseTile is Tile t && t.sprite) ? t.sprite : tilemap.GetSprite(pos);
            if (!sprite) continue;

            var cellWorldPos = tilemap.GetCellCenterWorld(pos);

            var go = new GameObject($"FurnCol_{x}_{y}", typeof(BoxCollider));
            go.layer = layer;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = cellWorldPos;

            //[cite_start]// (Gemini) Kodun bu kısmı GameStart.cs'ten alındı [cite: 144-146]
            var size = sprite.bounds.size;
            var center = sprite.bounds.center;

            var col = go.GetComponent<BoxCollider>();
            col.center = new Vector3(center.x, center.y, 0f);
            
            //[cite_start]// (Gemini) Düzeltme: GameStart.cs'teki 0.5f yerine [cite: 148]
            // bizim 'colliderDepth' değişkenimizi kullan.
            col.size = new Vector3(size.x, size.y, colliderDepth); 

            if (markStatic) go.isStatic = true;
        }
    }

    // LayerMask (örn: 00010000) değerini int (örn: 4) index'ine çeviren yardımcı metod
    private int LayerMaskToLayerIndex(LayerMask layerMask)
    {
        int layerNumber = layerMask.value;
        int layerIndex = 0;
        
        // Layer 'Nothing' (0) ise -1 döndür
        if (layerNumber == 0) return -1; 

        // Layer 'Everything' (-1) ise 0 döndür (Default)
        if (layerNumber == -1) return 0;
        
        // Bit kaydırarak ilk '1'i bul
        while (layerNumber > 1)
        {
            layerNumber = layerNumber >> 1;
            layerIndex++;
        }
        
        // 'Default' (0) veya atanmamışsa
        if (layerIndex > 31) return 0; 
            
        return layerIndex;
    }
}