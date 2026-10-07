using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>Cartoon button feel: grows and wiggles on hover, squashes when pressed.</summary>
    public class UIBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.07f;
        Selectable selectable;
        bool hover, down;
        float scale = 1f, velocity, wiggle;

        void Awake() => selectable = GetComponent<Selectable>();

        bool Interactable => selectable == null || selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData e) { hover = true; if (Interactable) wiggle = 1f; }
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        void OnDisable()
        {
            hover = down = false;
            scale = 1f;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float target = !Interactable ? 1f : down ? 0.92f : hover ? hoverScale : 1f;
            // Springy scale
            velocity += (target - scale) * 400f * dt;
            velocity *= Mathf.Exp(-18f * dt);
            scale += velocity * dt;
            wiggle = Mathf.MoveTowards(wiggle, 0f, dt * 3f);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 30f) * 3f * wiggle);
        }
    }
}
