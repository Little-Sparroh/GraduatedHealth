public static class EnemyHealthHelpers
{
    public static bool TryGetEnemyMaxHealth(EnemyBrain brain, out float max)
    {
        max = 0f;
        if (brain == null || brain.Core == null)
            return false;

        var coreOnly = false;
        try
        {
            if (brain.EnemyClass != null)
                coreOnly = brain.EnemyClass.config.onlyUseCoreHealthForHealthbar;
        }
        catch
        {
            coreOnly = false;
        }

        if (coreOnly)
        {
            max = brain.Core.MaxHealth;
            return max > 0f;
        }

        var current = 0f;
        SumShellHealth(brain.Core, ref current, ref max);
        if (max <= 0f)
            max = brain.Core.MaxHealth;
        return max > 0f;
    }

    public static void SumShellHealth(IEnemyComponent component, ref float current, ref float max)
    {
        if (component == null)
            return;

        if (component is EnemyPart part)
            if ((part.ComponentType & EnemyComponentType.Shell) != 0)
            {
                max += part.MaxHealth;
                if (part.IsAlive)
                    current += part.Health;
            }

        var children = component.ChildComponents;
        if (children == null)
            return;

        for (var i = 0; i < children.Count; i++)
            SumShellHealth(children[i], ref current, ref max);
    }
}