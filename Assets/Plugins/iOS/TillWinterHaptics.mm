// Till Winter haptics bridge (iOS 10+). Called from TillWinter.Unity.Haptics through DllImport("__Internal").
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *tw_light = nil;
static UIImpactFeedbackGenerator *tw_medium = nil;
static UIImpactFeedbackGenerator *tw_heavy = nil;
static UISelectionFeedbackGenerator *tw_selection = nil;

static UIImpactFeedbackGenerator *TW_Generator(int style)
{
    switch (style)
    {
        case 0:
            if (tw_light == nil) { tw_light = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [tw_light prepare]; }
            return tw_light;
        case 1:
            if (tw_medium == nil) { tw_medium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium]; [tw_medium prepare]; }
            return tw_medium;
        default:
            if (tw_heavy == nil) { tw_heavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy]; [tw_heavy prepare]; }
            return tw_heavy;
    }
}

extern "C"
{
    void TW_HapticImpact(int style, float intensity)
    {
        UIImpactFeedbackGenerator *g = TW_Generator(style);
        if (@available(iOS 13.0, *)) [g impactOccurredWithIntensity:intensity];
        else [g impactOccurred];
        [g prepare];
    }

    void TW_HapticSelection()
    {
        if (tw_selection == nil) { tw_selection = [[UISelectionFeedbackGenerator alloc] init]; [tw_selection prepare]; }
        [tw_selection selectionChanged];
        [tw_selection prepare];
    }
}
