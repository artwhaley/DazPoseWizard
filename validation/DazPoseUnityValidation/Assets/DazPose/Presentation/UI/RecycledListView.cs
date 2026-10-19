using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using DazPose.Game;
namespace DazPose.Presentation
{
    // Only viewport-sized rows exist. Filtering changes the index list, never the edit draft.
    public sealed class RecycledListView : MonoBehaviour
    {
        public ScrollRect scroll;
        public RectTransform content;
        public float rowHeight=72;
        readonly List<GameObject> rows=new List<GameObject>();
        string[] itemIds=Array.Empty<string>();
        readonly Dictionary<GameObject,string> rowIds=new Dictionary<GameObject,string>();
        string focusedId,focusedControl;
        GameObject template;
        void CaptureFocus() {
            var selected=EventSystem.current?.currentSelectedGameObject;if(selected==null)return;
            foreach(var pair in rowIds)if(pair.Key!=null&&selected.transform.IsChildOf(pair.Key.transform)) {
                focusedId=pair.Value;focusedControl=ControlPath(pair.Key.transform,selected.transform);return;
            }
            focusedId=focusedControl=null;
        }
        static string ControlPath(Transform row,Transform control) {
            var parts=new List<string>();while(control!=row){parts.Add(control.name);control=control.parent;}parts.Reverse();return string.Join("/",parts);
        }
        void RestoreFocus() {
            if(focusedId==null||EventSystem.current==null)return;
            foreach(var pair in rowIds)if(pair.Value==focusedId&&pair.Key.activeSelf) {
                var target=pair.Key.transform.Find(focusedControl);if(target!=null&&target.GetComponent<Selectable>()!=null)EventSystem.current.SetSelectedGameObject(target.gameObject);return;
            }
            // A pooled row may now represent another ID. Never leave keyboard input on it.
            var selected=EventSystem.current.currentSelectedGameObject;
            if(selected!=null&&rows.Any(r=>selected.transform.IsChildOf(r.transform)))EventSystem.current.SetSelectedGameObject(null);
        }
        public void SetCatalogItems(string[] ids,GameObject prefab,Action<int,GameObject> binder,bool resetScroll) {
            CaptureFocus();float y=Mathf.Max(0,content.anchoredPosition.y);int top=Mathf.FloorToInt(y/rowHeight);float offset=y-top*rowHeight;
            int anchor=CatalogViewportAnchor.Resolve(itemIds,ids,top);
            if(focusedId!=null&&!ids.Contains(focusedId)) {
                int index=Array.IndexOf(itemIds,focusedId);focusedId=ids.Length==0?null:ids[CatalogViewportAnchor.Resolve(itemIds,ids,index)];
            }
            bool same=itemIds.SequenceEqual(ids);itemIds=ids;
            if(!resetScroll)content.anchoredPosition=new Vector2(content.anchoredPosition.x,Mathf.Clamp(anchor*rowHeight+offset,0,Mathf.Max(0,ids.Length*rowHeight-scroll.viewport.rect.height)));
            changingItems=true;try{if(same&&!resetScroll){bind=binder;RefreshBindings();return;}SetItems(ids.Length,prefab,binder,resetScroll);}finally{changingItems=false;}
        }
        Action<int,GameObject> bind;
        int count,lastFirst=-1;
        float lastHeight=-1;
        bool refresh;
        bool changingItems;
        public int LiveRowCount=>rows.Count;
        public void RefreshBindings() { lastFirst=-1;refresh=true;RefreshVisible(true); }
        void OnEnable() { if(scroll!=null)scroll.onValueChanged.AddListener(Scrolled);refresh=true; }
        void OnDisable() { if(scroll!=null)scroll.onValueChanged.RemoveListener(Scrolled); }
        void Scrolled(Vector2 _) { RefreshVisible(false); }
        public void SetItems(int itemCount,GameObject rowPrefab,Action<int,GameObject> binder,bool resetScroll=true)
        {
            if(itemCount<0 || rowPrefab==null || binder==null)throw new ArgumentException("A row prefab and valid item source are required.");
            if(template!=null && template!=rowPrefab) {foreach(var row in rows)if(row!=null)Destroy(row);rows.Clear();}
            count=itemCount;template=rowPrefab;bind=binder;
            content.sizeDelta=new Vector2(content.sizeDelta.x,count*rowHeight);
            if(resetScroll) {content.anchoredPosition=Vector2.zero;scroll.verticalNormalizedPosition=1;}
            lastFirst=-1;refresh=true;RefreshVisible(true);
        }
        void LateUpdate()
        {
            if(scroll==null || scroll.viewport==null)return;
            float height=scroll.viewport.rect.height;
            if(refresh || Mathf.Abs(height-lastHeight)>.5f)RefreshVisible(true);
        }
        void RefreshVisible(bool force)
        {
            if(template==null || bind==null || scroll==null || scroll.viewport==null || rowHeight<=0)return;
            float height=Mathf.Max(0,scroll.viewport.rect.height);
            int capacity=Mathf.Min(count,Mathf.CeilToInt(height/rowHeight)+2);
            int first=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0,content.anchoredPosition.y)/rowHeight),0,Mathf.Max(0,count-capacity));
            if(!force && first==lastFirst && Mathf.Abs(height-lastHeight)<.5f)return;
            if(!changingItems)CaptureFocus();lastFirst=first;lastHeight=height;refresh=false;rowIds.Clear();
            var library=ProductionUiLibrary.Load();
            while(rows.Count<capacity)
            {
                var row=Instantiate(template,content,false);
                library.Prepare(row);rows.Add(row);
            }
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];bool visible=i<capacity && first+i<count;
                if(!visible){row.SetActive(false);continue;}
                var rect=(RectTransform)row.transform;
                rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);
                rect.anchoredPosition=new Vector2(0,-(first+i)*rowHeight);rect.sizeDelta=new Vector2(0,rowHeight-4);
                bind(first+i,row);row.SetActive(true);if(first+i<itemIds.Length)rowIds[row]=itemIds[first+i];
            }
            RestoreFocus();
        }
    }
}
