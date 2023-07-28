namespace Roost.Piebald
{
    using System;
    using UnityEngine;

    [RequireComponent(typeof(RectTransform))]
    public class SpinAnimation : MonoBehaviour
    {
        public const float DefaultSpeed = -360 / 16;

        private bool isRunning = true;

        private RectTransform rectTransform;

        public bool IsRunning
        {
            get => this.isRunning;
            set => this.isRunning = value;
        }

        public float Speed { get; set; } = DefaultSpeed;

        public SpinAnimation SetSpeed(float speed)
        {
            this.Speed = speed;
            return this;
        }

        public SpinAnimation Start()
        {
            this.isRunning = true;
            return this;
        }

        public SpinAnimation Stop()
        {
            this.isRunning = false;
            return this;
        }

        private void Awake()
        {
            this.rectTransform = this.GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (!this.isRunning)
            {
                return;
            }

            this.rectTransform.Rotate(0f, 0f, this.Speed * Time.deltaTime);
        }
    }

    public static class SpinAnimationWidgetExtensions
    {
        public static TWidget WithSpinAnimation<TWidget>(this TWidget widget, float degreesPerSecond, out SpinAnimation spinAnimation)
            where TWidget : UIGameObjectWidget
        {
            widget.WithBehavior<SpinAnimation>(out spinAnimation);
            spinAnimation.Speed = degreesPerSecond;
            return widget;
        }

        public static TWidget WithSpinAnimation<TWidget>(this TWidget widget, float degreesPerSecond, Action<SpinAnimation> configure = null)
            where TWidget : UIGameObjectWidget
        {
            widget.WithBehavior<SpinAnimation>(spin =>
            {
                spin.Speed = degreesPerSecond;
                configure?.Invoke(spin);
            });

            return widget;
        }
    }
}
