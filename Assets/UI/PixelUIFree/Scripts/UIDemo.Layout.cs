using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PixelUIFree
{
    /// <summary>The demo's screen: a HUD, a settings window, an inventory and a line of dialogue.</summary>
    public partial class UIDemo
    {
        static readonly Color Background = new Color(0.07f, 0.07f, 0.1f);

        static Texture2D Backdrop() => null;

        void Layout()
        {
            Hud();
            Settings();
            Inventory();
            Dialog();
            Switcher(6, 220, 2);
        }

        void Hud()
        {
            var hearts = Place(Row(screen, 0), 6, 5);
            foreach (var name in new[] { "heart", "heart", "heart", "heart_half", "heart_empty" })
                Icon(hearts, name);
            var bars = Place(Column(screen, 2), 92, 5);
            Bar(bars, "Red", 70, 90, 8);
            Bar(bars, "Blue", 45, 90, 8);
            var money = Place(Row(screen, 1), 300, 5);
            foreach (var (icon, text) in new[] { ("coin", "1250"), ("gem", "12"), ("key", "3") })
            {
                Icon(money, icon);
                Label(money, text);
                Gap(money, 4);
            }
        }

        void Settings()
        {
            var box = Window("SETTINGS", 6, 28, 148);
            Slider(box, "Music", 70, 34);
            Slider(box, "Sound", 40, 34);
            Check(box, "Fullscreen", true);
            Check(box, "Show hints", false);
            Dropdown(box, 1, "Easy", "Normal", "Hard");
            Input(box, "Hero name");
            var buttons = Row(box);
            foreach (var t in new[] { "Back", "Apply" })
                Expand(Button(buttons, t));
        }

        void Inventory()
        {
            var box = Window("INVENTORY", 162, 28, 258);
            var pages = Tabs(box, "Items", "Gear");
            var items = Grid(pages[0], 9, 2);
            var stock = new[] { ("potion", 3), ("potion_blue", 2), ("potion_green", 1), ("apple", 5), ("bomb", 4),
                ("key", 1), ("gem", 12), ("gem_red", 2), ("gem_green", 0), ("coin_silver", 30), ("star", 0),
                ("leaf", 7), ("flame", 0), ("lightning", 0), ("chest", 0), ("bag", 0), ("sword", 0), ("shield", 0) }
                .Where(p => Has(p.Item1)).Take(16).ToArray();
            for (int i = 0; i < 18; i++)
            {
                var slot = Make("InsetPanel", items);
                if (i >= stock.Length)
                    continue;
                var (name, count) = stock[i];
                Tip(Icon(slot, name), Title(name));
                var pad = slot.GetComponent<VerticalLayoutGroup>().padding;
                if (count > 1)
                    Over(slot, count.ToString(), new Vector2(pad.left + 19, pad.top + 10), true, null, Color.black);
            }
            foreach (var (icon, text) in new[] { ("sword", "Iron sword"), ("shield", "Oak shield") })
            {
                var r = Row(pages[1]);
                Icon(r, icon);
                Expand(Label(r, text));
                Button(r, "Equip");
            }
            var quest = Row(box);
            Icon(quest, "trophy");
            Label(quest, "Level 7");
            var xp = Bar(quest, "Gold", 60, 0, 8);
            Expand(xp);
            Centre((RectTransform)xp.transform);
            var actions = Row(box);
            foreach (var (icon, text) in new[] { ("check", "Use"), ("trash", "Drop"), ("info", "Info") })
                Expand(Button(actions, text, icon));
        }

        void Dialog()
        {
            var panel = Place(Make("Panel", screen), 6, 176);
            MinSize(panel, 414);
            var r = Row(panel, 6);
            Icon(Make("InsetPanel", r), "user");
            Expand(Label(r, "Welcome, traveller! The shop opens at dawn.\nBring coins, and mind the bombs."));
            Centre((RectTransform)Button(r, "", "arrow_right").transform);
        }
    }
}
