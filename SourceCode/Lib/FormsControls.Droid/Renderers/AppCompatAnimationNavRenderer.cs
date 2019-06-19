using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using FormsControls.Base;
using FormsControls.Droid;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Platform.Android;
using Xamarin.Forms.Platform.Android.AppCompat;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;
using FragmentTransaction = Android.Support.V4.App.FragmentTransaction;

[assembly: ExportRenderer(typeof(NavigationPage), typeof(AppCompatAnimationNavRenderer))]
namespace FormsControls.Droid
{
    public class AppCompatAnimationNavRenderer : NavigationPageRenderer
    {
        private const int TransitionDuration = 700;
        private readonly AppCompatNavRendererHelper _hepler;

        public AppCompatAnimationNavRenderer(Context context)
        : base(context)
        {
            _hepler = new AppCompatNavRendererHelper(this, context);
        }

        protected override void OnElementChanged(ElementChangedEventArgs<NavigationPage> e)
        {
            if (e.OldElement != null)
            {
                UnsubscribeFromNavigationEvents(e.OldElement);
            }
            if (e.NewElement != null)
            {
                _hepler.UnsubscribeFromStandardNavigationEvents(e.NewElement);
                SubscribeToNavigationEvents(e.NewElement);
            }
            base.OnElementChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Element != null)
            {
                UnsubscribeFromNavigationEvents(Element);
            }
        }

        private void SubscribeToNavigationEvents(INavigationPageController page)
        {
            page.PushRequested += OnPushedWithAnimation;
            page.PopRequested += OnPoppedWithAnimation;
            page.PopToRootRequested += OnPoppedToRootWithAnimation;
        }

        private void UnsubscribeFromNavigationEvents(INavigationPageController page)
        {
            page.PushRequested -= OnPushedWithAnimation;
            page.PopRequested -= OnPoppedWithAnimation;
            page.PopToRootRequested -= OnPoppedToRootWithAnimation;
        }

        private void OnPushedWithAnimation(object sender, NavigationRequestedEventArgs e)
        {
            e.Task = SwitchContentAsync(e.Page, e.Animated);
        }

        private void OnPoppedWithAnimation(object sender, NavigationRequestedEventArgs e)
        {
            var pageToShow = ((INavigationPageController)Element).Pages.Skip(1).FirstOrDefault();
            e.Task = pageToShow == null ? Task.FromResult(false) : SwitchContentAsync(pageToShow, e.Animated, true);
        }

        private void OnPoppedToRootWithAnimation(object sender, NavigationRequestedEventArgs e)
        {
            e.Task = SwitchContentAsync(e.Page, e.Animated, true, true);
        }

        private double GetAnimationDuration(IPageAnimation animation)
        {
            switch (animation.Duration)
            {
                case AnimationDuration.Short:
                    return Durations.ShortDuration;
                case AnimationDuration.Medium:
                    return Durations.MediumDuration;
                case AnimationDuration.Long:
                    return Durations.LongDuration;
                default:
                    return Durations.ZeroDuration;
            }
        }

        private Task<bool> SwitchContentAsync(Page page, bool animated, bool removed = false, bool popToRoot = false)
        {
            var tcs = new TaskCompletionSource<bool>();
            Fragment fragment = _hepler.GetFragment(page, removed, popToRoot);
            List<Fragment> fragments = _hepler.FragmentStack;
            FragmentManager fm = _hepler.FragmentManager;
            var currentPade = (removed ? _hepler.CurrentPage : page);
            var currentAnimPade = currentPade as IAnimationPage;
            var animation = AnimationNavigationPage.GetAnimation(currentPade, animated);
            _hepler.CurrentPage = page;
            _hepler.SetNavAnimationInProgress(Element, true);
            FragmentTransaction transaction = fm.BeginTransaction();
            AnimationHelper.SetupTransition(transaction, animation, !removed, animated);
            if (animation.Type != AnimationType.Empty && animation.Duration != AnimationDuration.Zero)
            {
                currentAnimPade?.OnAnimationStarted(removed);
            }
            transaction.DisallowAddToBackStack();
            if (fragments.Count == 0)
            {
                transaction.Add(Id, fragment);
                fragments.Add(fragment);
            }
            else
            {
                if (removed)
                {
                    // pop only one page, or pop everything to the root
                    var popPage = true;
                    var fragmentsToRemove = new List<Fragment>();
                    while (fragments.Count > 1 && popPage)
                    {
                        var currentToRemove = fragments.Last();
                        fragments.RemoveAt(fragments.Count - 1);
                        fragmentsToRemove.Add(currentToRemove);
                        transaction.Hide(currentToRemove);
                        popPage = popToRoot;
                    }
                    // we need it for poping pages with animation
                    RemoveFragments(fragmentsToRemove);
                    Fragment toShow = fragments.Last();
                    // Execute pending transactions so that we can be sure the fragment list is accurate.
                    fm.ExecutePendingTransactions();
                    if (fm.Fragments.Contains(toShow))
                        transaction.Show(toShow);
                    else
                        transaction.Add(Id, toShow);
                }
                else
                {
                    // push
                    Fragment currentToHide = fragments.Last();
                    transaction.Hide(currentToHide);
                    transaction.Add(Id, fragment);
                    fragments.Add(fragment);
                }
            }
            // We don't currently support fragment restoration, so we don't need to worry about
            // whether the commit loses state
            transaction.CommitAllowingStateLoss();

            // The fragment transitions don't really SUPPORT telling you when they end
            // There are some hacks you can do, but they actually are worse than just doing this:
            if (animated)
            {
                if (!removed)
                {
                    _hepler.UpdateToolbar();
                    if (_hepler.DrawerToggle != null && ((INavigationPageController)Element).StackDepth == 2)
                        _hepler.AnimateArrowIn();
                }
                else if (_hepler.DrawerToggle != null && ((INavigationPageController)Element).StackDepth == 2)
                    _hepler.AnimateArrowOut();
                Device.StartTimer(TimeSpan.FromMilliseconds(TransitionDuration), () =>
                {
                    tcs.TrySetResult(true);
                    fragment.UserVisibleHint = true;
                    if (removed)
                    {
                        _hepler.UpdateToolbar();
                    }
                    return false;
                });
            }
            else
            {
                Device.StartTimer(TimeSpan.FromMilliseconds(1), () =>
                {
                    tcs.TrySetResult(true);
                    fragment.UserVisibleHint = true;
                    _hepler.UpdateToolbar();
                    return false;
                });
            }
            Context.HideKeyboard(this);

            if (animation.Type != AnimationType.Empty && animation.Duration != AnimationDuration.Zero && currentAnimPade != null)
            {
                Device.StartTimer(TimeSpan.FromMilliseconds(GetAnimationDuration(currentAnimPade.PageAnimation)), delegate
                {
                    currentAnimPade.OnAnimationFinished(removed);
                    return false;
                });
            }
            _hepler.SetNavAnimationInProgress(Element, false);
            return tcs.Task;
        }

        private async void RemoveFragments(List<Fragment> fragmentsToRemove)
        {
            await Task.Delay(Context.Resources.GetInteger(Resource.Integer.animation_duration)).ConfigureAwait(true);
            FragmentManager fm = _hepler.FragmentManager;
            FragmentTransaction transaction = fm.BeginTransaction();
            transaction.DisallowAddToBackStack();
            foreach (var fragment in fragmentsToRemove)
            {
                transaction.Remove(fragment);
            }
            transaction.Commit();
        }
    }
}