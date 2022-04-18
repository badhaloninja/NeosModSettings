using FrooxEngine.UIX;
using FrooxEngine;

namespace NeosModSettings
{
    public interface INmsMod
    {
        void buildConfigUI(UIBuilder ui, Slot optionsRoot);
    }
}
