using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Michsky.MUIP
{
    public class CustomTwinButtonToggleParent : MonoBehaviour
    {
        public List<DemoElementSway> elements;
        public void DissolveAll(DemoElementSway currentSway)
        {
            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i] == currentSway)
                {
                    if (elements[i].gameObject.activeSelf) elements[i].Active();
                    continue;
                }

                if (elements[i].gameObject.activeSelf) elements[i].Dissolve();
            }
        }

        public void HighlightAll()
        {
            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i].gameObject.activeSelf) elements[i].Highlight();
            }
        }

        public void SetWindowManagerButton(int index)
        {
            // MOD:
            index = index == 0 ? 1 : 0;

            if (elements.Count == 0)
            {
                StartCoroutine("SWMHelper", index);
                return;
            }

            for (int i = 0; i < elements.Count; ++i)
            {
                if (i == index) { elements[i].WindowManagerSelect(); }
                else
                {
                    if (elements[i].wmSelected == false) { continue; }
                    elements[i].WindowManagerDeselect(); 
                }
            }
        }

        IEnumerator SWMHelper(int index)
        {
            yield return new WaitForSeconds(0.1f);
            SetWindowManagerButton(index);
        }
    }
}