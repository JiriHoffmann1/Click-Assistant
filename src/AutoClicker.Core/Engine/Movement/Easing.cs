namespace AutoClicker.Core.Engine.Movement;

public static class Easing
{
    public static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
}
