using System.Collections.Generic;

namespace Emotes;

public class EmotesConfig
{
    public bool PairedEmotesRequireConsent { get; set; } = true;
    public List<string> DisabledEmotes { get; set; } = new();
}
