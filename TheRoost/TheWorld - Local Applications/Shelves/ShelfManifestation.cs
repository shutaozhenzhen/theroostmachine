using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Constants;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Ghosts;
using SecretHistories.Manifestations;
using SecretHistories.Spheres;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Roost.World.Shelves
{
    [RequireComponent(typeof(RectTransform))]
    class ShelfManifestation : BasicManifestation, IManifestation, IPointerClickHandler, IPointerEnterHandler
    {
        // Currently hardcoded, should probably use a delegate to go take it from the tabletopchoreographer
        int GRID_WIDTH = 75;
        int GRID_HEIGHT = 140;

        public static int CELL_WIDTH = 75;
        public static int CELL_HEIGHT = 115;
        public static float H = 0;

        public static float COL_OFFSET =  -35f;
        public static float ROW_OFFSET = -57.5f;

        LineRenderer lr = null;
        public Shelf entity = null;
        float ShelfWidth = 0;
        float ShelfHeight = 0;

        TabletopChoreographer tc;

        public bool RequestingNoDrag => false;
        public bool RequestingNoSplit => true;

        public bool NoPush => true;

        public IGhost CreateGhost()
        {
            return NullGhost.Create(this);
        }

        public void Initialise(IManifestable manifestable)
        {
            tc = Watchman.Get<TabletopChoreographer>();
            
            // Here we build the actual visual representation of the zone, based on the ZoneData we receive in arg
            ShelfPayload sp = manifestable as ShelfPayload;
            entity = Watchman.Get<Compendium>().GetEntityById<Shelf>(sp.EntityId);

            ShelfWidth = entity.Columns * CELL_WIDTH;
            ShelfHeight = entity.Rows * CELL_HEIGHT;

            BuildMainShelf();

            for (int i = 0; i < entity.Areas.Count; i++)
            {
                ShelfArea area = entity.Areas[i];
                BuildArea(area, i);
            }

            ShelfMaster.RegisteredShelfManifestations.Add(this);
        }

        public override void Retire(RetirementVFX vfx, Action callbackOnRetired)
        {
            Destroy(gameObject);
            callbackOnRetired();
        }
        void OnDestroy()
        {
            ShelfMaster.RegisteredShelfManifestations.Remove(this);
        }

        void Update()
        {
            SetOutlinePositions();
        }

        void BuildMainShelf()
        {
            gameObject.AddComponent<LineRenderer>();
            lr = gameObject.GetComponent<LineRenderer>();
            SetOutlinePositions();

            lr.positionCount = 5;
            lr.material = new Material(Shader.Find("UI/Default"));
            lr.material.color = new Color32(138, 255, 247, 100);
            lr.startWidth = 2f;
            lr.sortingOrder = 1;
            lr.alignment = LineAlignment.TransformZ;

            RectTransform.sizeDelta = new Vector2(ShelfWidth, ShelfHeight);
            gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

/*            BuildBackgroundImage(
                gameObject.transform,
                entity.Background != "" ? entity.Background : "zone_bg",
                ShelfWidth,
                ShelfHeight,
                true,
                new Color32(3, 252, 236, 160)
            );*/
        }

        void BuildBackgroundImage(Transform parent, string bg, float width, float height, bool tiled, Color32 color)
        {
            GameObject goi = new GameObject("bg");
            goi.transform.SetParent(parent);

            Image i = goi.AddComponent<Image>();
            i.sprite = ResourcesManager.GetSpriteForUI(bg);// entity.Background != "" ? entity.Background : "zone_bg");
            if (tiled)
            {
                i.type = Image.Type.Tiled;
                i.pixelsPerUnitMultiplier = 2.5f;
            }
            i.color = color;
            var rt = goi.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2((width / 2) - (CELL_WIDTH / 2), (height / 2) - (CELL_HEIGHT / 2));
            rt.sizeDelta = new Vector2(width, height);
        }

        void BuildArea(ShelfArea area, int index)
        {
            GameObject areaObj = new GameObject($"shelfarea_{index}");
            areaObj.transform.SetParent(gameObject.transform);
            var rt = areaObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(area.Columns * CELL_WIDTH, area.Rows * CELL_HEIGHT);
            rt.anchoredPosition = new Vector2(
                (area.X-1) * CELL_WIDTH, 
                ((entity.Rows - area.Y)) * CELL_HEIGHT
            );

            BuildBackgroundImage(
                areaObj.transform, 
                area.Background != "" ? area.Background : "zone_bg", 
                area.Columns * CELL_WIDTH, 
                area.Rows * CELL_HEIGHT, 
                false,
                new Color32(255, 255, 255, 50)
            );
        }

        void SetOutlinePositions()
        {
            lr.SetPosition(0, new Vector3(
                transform.position.x + COL_OFFSET - 4 - 2,
                transform.position.y + ROW_OFFSET - 2,
                H)
            );

            lr.SetPosition(1, new Vector3(
                transform.position.x + COL_OFFSET + ShelfWidth,
                transform.position.y + ROW_OFFSET - 2,
                H
            ));

            lr.SetPosition(2, new Vector3(
                transform.position.x + COL_OFFSET + ShelfWidth,
                transform.position.y + ROW_OFFSET + ShelfHeight + 2,
                H
            ));

            lr.SetPosition(3, new Vector3(
                transform.position.x + COL_OFFSET - 4,
                transform.position.y + ROW_OFFSET + ShelfHeight + 2,
                H
            ));

            lr.SetPosition(4, new Vector3(
                transform.position.x + COL_OFFSET - 4,
                transform.position.y + ROW_OFFSET - 2,
                H)
            );
        }

        protected Rect GetSphereRect()
        {
            return tc.Sphere.GetRect();
        }

        Vector2 GetPosClampedToTable(Vector2 pos)
        {
            const float padding = .2f;

            var tableMinX = GetSphereRect().x + padding;
            var tableMaxX = GetSphereRect().x + GetSphereRect().width - padding;
            var tableMinY = GetSphereRect().y + padding;
            var tableMaxY = GetSphereRect().y + GetSphereRect().height - padding;
            pos.x = Mathf.Clamp(pos.x, tableMinX, tableMaxX);
            pos.y = Mathf.Clamp(pos.y, tableMinY, tableMaxY);
            return pos;
        }

        public bool IsPositionAvailable(Token token, Vector2 position)
        {
            Vector2 intendedPosClampedToTable = GetPosClampedToTable(position);
            Vector2 intendedPosOnGrid = tc.SnapToGrid(intendedPosClampedToTable, token, GRID_WIDTH, GRID_HEIGHT);
            var targetRect = token.GetRectFromPosition(intendedPosOnGrid);
            var legalPositionCheckResult = tc.IsLegalPlacement(targetRect, token);
            Birdsong.Sing(Birdsong.Incr(), entity.Id, "=> Position", position, "is it legal? ", legalPositionCheckResult.IsLegal);
            return legalPositionCheckResult.IsLegal;
        }

        public Vector2 ComputeCenterofAreaPosition(ShelfArea area, int posX, int posY)
        {

            return new Vector2(0, 0);
        }

        public Vector2 NextAvailablePosition(ShelfArea area, Token token)
        {
            Birdsong.Sing(Birdsong.Incr(), entity.Id, "=> Someone asked if area", area.Id, "is filled.");
            for(var x=0; x < area.X; x++)
            {
                for(var y=0; y < area.Y; y++)
                {
                    Birdsong.Sing(Birdsong.Incr(), entity.Id, "=> Checking relative position [", x, ";", y, "]...");
                    //Compute position on the board, based on shelf + area positions
                    Vector2 centerOfPosition = ComputeCenterofAreaPosition(area, x, y);
                    // Is legal? If so, return. If not, continue
                    if (IsPositionAvailable(token, centerOfPosition)) return centerOfPosition;
                }
            }
            return Vector2.negativeInfinity;
        }

        public void FillArea(ShelfArea area, List<Token> tokens, List<Token> generalList)
        {
            Birdsong.Sing(Birdsong.Incr(), entity.Id,  "=> Area", area.Id, "was considered free, trying to fill it");
        }

        public OccupiesSpaceAs OccupiesSpaceAs() => SecretHistories.Enums.OccupiesSpaceAs.Meta;

        public void Emphasise() { }
        public void Understate() {}

        public void Highlight(HighlightType highlightType, IManifestable manifestable) { }
        public void Unhighlight(HighlightType highlightType, IManifestable manifestable) {}

        public void Shroud(bool instant) { }
        public void Unshroud(bool instant) {}

        public void UpdateVisuals(IManifestable manifestable, Sphere sphere)
        {
            base.gameObject.transform.parent.SetAsFirstSibling();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
        }

        public override bool HandlePointerClick(PointerEventData eventData, Token token)
        {
            return false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ExecuteEvents.Execute<IPointerEnterHandler>(transform.parent.gameObject, eventData,
                (parentToken, y) => parentToken.OnPointerEnter(eventData));
        }
    }
}
