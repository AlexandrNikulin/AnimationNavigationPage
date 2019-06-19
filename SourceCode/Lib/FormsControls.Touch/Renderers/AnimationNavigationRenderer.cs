using System.Reflection;
using System.Threading.Tasks;
using FormsControls.Base;
using FormsControls.Touch;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly: ExportRenderer(typeof(AnimationNavigationPage), typeof(AnimationNavigationRenderer))]

namespace FormsControls.Touch
{
    public class AnimationNavigationRenderer : NavigationRenderer
    {
        private readonly IPageAnimation _popToRootAnimation = new SlidePageAnimation();
        private readonly UISwipeGestureRecognizer _swipeLeftRecognizer;
        private readonly PushPageAnimation _defaultAnimation = new PushPageAnimation();
        private readonly PropertyInfo _currentPagePropertyInfo = typeof(NavigationRenderer).GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly NavigationControllerDelegate _delegate = new NavigationControllerDelegate();
        private bool _useDefaultAnimation;
       
        public AnimationNavigationRenderer()
        {
            Delegate = _delegate;
            _swipeLeftRecognizer = new UISwipeGestureRecognizer(SwipeToGoBack)
            {
                Direction = UISwipeGestureRecognizerDirection.Right,
                Delegate = new GestureRecognizerDelegate()
            };
        }

        private Page CurrentPage => _currentPagePropertyInfo.GetValue(this) as Page;

        private void SwipeToGoBack()
        {
            _useDefaultAnimation = true;
            PopViewController(true);
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            InteractivePopGestureRecognizer.Enabled = false;
            if ((Element as AnimationNavigationPage).EnableInteractivePopGesture)
            {
                View.AddGestureRecognizer(_swipeLeftRecognizer);
            }
        }

        public override UIViewController[] PopToRootViewController(bool animated)
        {
            if (animated)
            {
                if(_delegate.Animation.Type != AnimationType.Push)
                    _delegate.Animation = _popToRootAnimation;
            }
            else
            {
                _delegate.Animation.Duration = 0;
            }
            return base.PopToRootViewController(animated);
        }

        public override UIViewController PopViewController(bool animated)
        {
            CurrentPage.Navigation.PopAsync();
            return this;
        }

        protected override Task<bool> OnPushAsync(Page page, bool animated)
        {
            return base.OnPushAsync(page, ExecuteAnimation(page, animated, false));
        }

        protected override Task<bool> OnPopViewAsync(Page page, bool animated)
        {
            return base.OnPopViewAsync(page, ExecuteAnimation(page, animated, true));
        }

        private IPageAnimation GetPageAnimation(Page page, bool animated)
        {
            var animation = _useDefaultAnimation ? _defaultAnimation : AnimationNavigationPage.GetAnimation(page, animated);
            _useDefaultAnimation = false;
            return animation;
        }

        private bool ExecuteAnimation(Page page, bool animated, bool isPop)
        {
            var currentPage = page as IAnimationPage;
            var animation = GetPageAnimation(page, animated);
            _delegate.Animation = animation;
            _delegate.CurrentPage = currentPage;
            _delegate.IsPopAnimation = isPop;
            _delegate.AnimateNavigationBar = (Element as AnimationNavigationPage).AnimateNavigationBar;
            View.BackgroundColor = page.BackgroundColor.ToUIColor();
            if (animation.Duration > 0)
            {
                if (animation.Type == AnimationType.Flip)
                {
                    AnimationHelper.ExecuteAnimation(currentPage, View, animation, isPop);
                }
                return animation.Type != AnimationType.Flip;
            }
            return false;
        }
    }
}