namespace Nop.Plugin.CW.Core.Infrastructure
{
    public static class DependencyRegistrarConstants
    {
        // NOP Core's dependency registrar's Order is 2002, we set this as the next value possible
        public const int BASE_ORDER = 3002;

        public static int OrderAfterBaseRegistrar
        {
            get
            {
                return BASE_ORDER + 1;
            }
        }
    }
}