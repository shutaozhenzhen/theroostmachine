using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Choreographers;
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
        int GRID_HEIGHT = 115;//140?

        public static float CELL_WIDTH = 75;
        public static float CELL_HEIGHT = 115;
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
            //tc = Watchman.Get<TabletopChoreographer>();
            tc = ShelfMaster.GetTabletopSphere().gameObject.GetComponent<TabletopChoreographer>();
            
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
            if(!entity.NoOutline) BuildOutline();

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
                area.Background == "",
                new Color32(255, 255, 255, 50)
            );
        }

        void BuildOutline()
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
        }

        void SetOutlinePositions()
        {
            lr.SetPosition(0, new Vector3(
                transform.position.x + COL_OFFSET - 2,
                transform.position.y + ROW_OFFSET - 1,
                H)
            );

            lr.SetPosition(1, new Vector3(
                transform.position.x + COL_OFFSET + ShelfWidth-2,
                transform.position.y + ROW_OFFSET - 1,
                H
            ));

            lr.SetPosition(2, new Vector3(
                transform.position.x + COL_OFFSET + ShelfWidth - 2,
                transform.position.y + ROW_OFFSET + ShelfHeight + 1,
                H
            ));

            lr.SetPosition(3, new Vector3(
                transform.position.x + COL_OFFSET - 1,
                transform.position.y + ROW_OFFSET + ShelfHeight + 1,
                H
            ));

            lr.SetPosition(4, new Vector3(
                transform.position.x + COL_OFFSET - 1,
                transform.position.y + ROW_OFFSET - 1,
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

        public Rect GetRectForToken(Token token)
        {
            Vector2 tokenPosition = token.gameObject.transform.position;
            return new Rect(
                    tokenPosition.x,
                    tokenPosition.y,
                    75,
                    115
                );
        }

        public Rect GetRectFromPosition(Vector2 position)
        {
            return new Rect(
                    position.x,
                    position.y,
                    75,
                    115
                );
        }

        public LegalPositionCheckResult IsLegalPlacement(Rect candidateRect, Token placingToken)
        {
            //Is the candidaterect inside the larger tabletop rect? if not, throw it out now.
            if (!GetSphereRect().Overlaps(candidateRect))
                return LegalPositionCheckResult.Illegal();

            Rect otherTokenOverlapRect;
            
            foreach (var otherToken in tc.Sphere.Tokens.Where(t => t != placingToken && t.OccupiesSameSpaceAs(placingToken) && !CanTokenBeIgnored(t)))
            {
                otherTokenOverlapRect = GetRectForToken(otherToken);
                Birdsong.Sing(Birdsong.Incr(), $"IsLegal=>Testing overlap between {placingToken.PayloadEntityId} ({candidateRect}) and {otherToken.PayloadEntityId} ({otherTokenOverlapRect}). overlap? {otherTokenOverlapRect.Overlaps(candidateRect)}");
                if (otherTokenOverlapRect.Overlaps(candidateRect))
                    return LegalPositionCheckResult.Blocked(otherToken.name, otherTokenOverlapRect);
            }
            return LegalPositionCheckResult.Legal();
        }
        public virtual bool CanTokenBeIgnored(Token token)
        {

            if (token.Defunct)
                return true;
            if (token.NoPush)
                return true;

            return false;
        }

        public bool IsPositionAvailable(Token token, Vector2 position)
        {
            Rect cell = GetRectFromPosition(position);
            //Birdsong.Sing(Birdsong.Incr(), "rough pos=", position);
            //Vector2 intendedPosClampedToTable = GetPosClampedToTable(position);
            //Vector2 intendedPosOnGrid = tc.SnapToGrid(intendedPosClampedToTable, null, GRID_WIDTH, GRID_HEIGHT);
            //Birdsong.Sing(Birdsong.Incr(), "Intended pos=", intendedPosOnGrid);
            //var targetRect = GetRectFromPosition(intendedPosOnGrid);
            Birdsong.Sing(Birdsong.Incr(), $"Position={position}, Rect={cell}");
            var legalPositionCheckResult = IsLegalPlacement(cell, token);
            Birdsong.Sing(Birdsong.Incr(), entity.Id, "=> Position", cell, "is it legal? ", legalPositionCheckResult.IsLegal);
            return legalPositionCheckResult.IsLegal;
        }

        public Vector2 ComputeCoordinateOfCell(ShelfArea area, int posX, int posY)
        {
            float x = gameObject.transform.position.x + (area.X-1) * CELL_WIDTH + posX * CELL_WIDTH;
            float y = gameObject.transform.position.y + (entity.Rows - area.Y) * CELL_HEIGHT - posY * CELL_HEIGHT;
            return new Vector2(x, y);
        }

        public Vector2 NextAvailablePosition(ShelfArea area, Token token)
        {
            Birdsong.Sing(Birdsong.Incr(), entity.Id, $"=> Someone asked for a valid position to fill in the area {area.Id}");
            for(var y=0; y < area.Rows; y++)
            {
                for(var x=0; x < area.Columns; x++)
                {
                    //Birdsong.Sing(Birdsong.Incr(), entity.Id, "=> Checking relative position [", x, ";", y, "]...");
                    //Compute position on the board, based on shelf + area positions
                    Vector2 coordinateOfCell = ComputeCoordinateOfCell(area, x, y);
                    // Is legal? If so, return. If not, continue
                    if (IsPositionAvailable(token, coordinateOfCell))
                    {
                        Birdsong.Sing(Birdsong.Incr(), $"Relative position {x} {y} is free!");
                        return coordinateOfCell;
                    }
                    else
                    {
                        Birdsong.Sing(Birdsong.Incr(), $"Relative position {x} {y} isn't free.");
                    }
                }
            }
            Birdsong.Sing(Birdsong.Incr(), "This area is full.");
            return Vector2.negativeInfinity;
        }

        public void FillArea(ShelfArea area, List<Token> tokens, List<Token> generalList)
        {
            Rect areaRect = new Rect(
                gameObject.transform.position.x + (area.X - 1) * CELL_WIDTH,
                gameObject.transform.position.y + (entity.Rows - area.Y) * CELL_HEIGHT,
                area.Columns * CELL_WIDTH,
                area.Rows * CELL_HEIGHT
            );
            Birdsong.Sing(Birdsong.Incr(), entity.Id,  $"=> Trying to fill the area {area.Id}");
            foreach(Token token in tokens) {
                // If you're already in the area, stay there (tokens can only contain expression-matching tokens)
                
                Rect tokenRect = GetRectForToken(token);

                Birdsong.Sing(Birdsong.Incr(), $"Will try to move {token.PayloadEntityId} in there. But first, let's check, maybe it's already there...");
                Birdsong.Sing(Birdsong.Incr(), $"GO pos={gameObject.transform.position}, rows-y={entity.Rows - area.Y}");
                Birdsong.Sing(Birdsong.Incr(), $"Area Rect {areaRect}, token rect {tokenRect}");
                if(areaRect.Overlaps(tokenRect))
                {
                    Birdsong.Sing(Birdsong.Incr(), $"Token {token.PayloadEntityId} is already in the area, let's not try to move it");
                    generalList.Remove(token);
                    continue;
                }
                else
                {
                    Birdsong.Sing(Birdsong.Incr(), "Looks like it's not. Maybe there's a free space for it.");
                }

                Vector2 newPos = NextAvailablePosition(area, token);
                if (newPos.Equals(Vector2.negativeInfinity))
                {
                    Birdsong.Sing(Birdsong.Incr(), $"Well, looks like area {area.Id} is full right now. Let's stop trying to fill it.");
                    return;
                }
                else
                {
                    Birdsong.Sing(Birdsong.Incr(), $"There's a space! {newPos}. Let's move the card.");
                    token.gameObject.transform.position = newPos;
                    generalList.Remove(token);
                }
            }
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
