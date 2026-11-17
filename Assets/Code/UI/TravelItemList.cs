using System;
using System.Collections.Generic;
using UnityEngine;
namespace TravelApp
{
    public sealed class TravelItemList : MonoBehaviour
    {
        [Tooltip("Prefab to instantiate. All styling lives in this reusable asset.")]
        public TravelItemView itemPrefab;
        public Transform content;
        [Tooltip("Saved sample card visible while editing; hidden when real data is displayed.")]
        public GameObject editorPreview;
        public GameObject emptyState;
        readonly List<TravelItemView> items = new List<TravelItemView>();
        public int VisibleCount { get; private set; }
        public void Render(int count, Action<TravelItemView, int> bind)
        {
            if (editorPreview != null) editorPreview.SetActive(false);
            if (emptyState != null) emptyState.SetActive(count == 0);
            for (int i = items.Count; i < count; i++) items.Add(Instantiate(itemPrefab, content));
            for (int i = 0; i < items.Count; i++)
            {
                items[i].gameObject.SetActive(i < count);
                if (i < count) bind(items[i], i);
            }
            VisibleCount = count;
        }
    }
}
