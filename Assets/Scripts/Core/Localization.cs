using System.Collections.Generic;

namespace MutantPlants
{
    /// <summary>
    /// Minimal English / Hungarian string table. Use L.T("key") or L.T("key", args).
    /// </summary>
    public static class L
    {
        public enum Language { English, Hungarian }

        public static Language Current = Language.English;

        static readonly Dictionary<string, (string en, string hu)> table = new Dictionary<string, (string, string)>
        {
            // Main menu
            ["title"] = ("MUTANT PLANTS", "MUTANT PLANTS"),
            ["subtitle"] = ("The fertilizer experiment went wrong. Defend the farm!", "A műtrágyázási kísérlet balul sült el. Védd meg a farmot!"),
            ["newGame"] = ("New game", "Új játék"),
            ["continue"] = ("Continue (checkpoint)", "Folytatás (checkpoint)"),
            ["settings"] = ("Settings", "Beállítások"),
            ["quit"] = ("Quit", "Kilépés"),
            ["leaderboard"] = ("TOP SCORES", "LEGJOBB PONTSZÁMOK"),
            ["noScores"] = ("No scores yet - go save the farm!", "Még nincs eredmény - mentsd meg a farmot!"),
            ["controls"] = ("WASD move  •  Mouse aim  •  Left click shoot  •  1-4 / wheel switch weapon  •  Shift sprint  •  Space jump  •  Esc pause",
                            "WASD mozgás  •  Egér célzás  •  Bal klikk lövés  •  1-4 / görgő fegyverváltás  •  Shift futás  •  Space ugrás  •  Esc szünet"),

            // Settings
            ["sensitivity"] = ("Mouse sensitivity", "Egérérzékenység"),
            ["volume"] = ("Effects volume", "Effekt hangerő"),
            ["music"] = ("Music volume", "Zene hangerő"),
            ["fov"] = ("Field of view", "Látószög"),
            ["invertY"] = ("Invert mouse Y", "Egér Y tengely fordítása"),
            ["fullscreen"] = ("Fullscreen", "Teljes képernyő"),
            ["shake"] = ("Screen shake", "Képernyőrázás"),
            ["language"] = ("Language", "Nyelv"),
            ["langName"] = ("English", "Magyar"),
            ["saveBack"] = ("Save & Back", "Mentés és vissza"),

            // HUD
            ["score"] = ("SCORE  {0} / {1}", "PONT  {0} / {1}"),
            ["hp"] = ("HP  {0}", "ÉLET  {0}"),
            ["locked"] = ("LOCKED", "ZÁROLVA"),
            ["wave"] = ("WAVE {0}", "{0}. HULLÁM"),
            ["waveCleared"] = ("WAVE CLEARED!", "HULLÁM TELJESÍTVE!"),
            ["nextWave"] = ("Next wave in {0}", "Következő hullám: {0}"),
            ["bossIncoming"] = ("THE PUMPKIN KING RISES!", "FELKELT A TÖKKIRÁLY!"),
            ["bossName"] = ("PUMPKIN KING", "TÖKKIRÁLY"),
            ["combo"] = ("COMBO x{0}", "KOMBÓ x{0}"),
            ["enemiesLeft"] = ("Mutants left: {0}", "Hátralévő mutánsok: {0}"),
            ["hint"] = ("Barn = checkpoint  •  ESC: pause", "Pajta = checkpoint  •  ESC: szünet"),
            ["dmgBoost"] = ("DOUBLE DAMAGE {0}s", "DUPLA SEBZÉS {0}mp"),
            ["rapidFire"] = ("RAPID FIRE {0}s", "GYORSTÜZELÉS {0}mp"),
            ["tut1"] = ("Mutant plants are coming! Shoot them to score points.", "Jönnek a mutáns növények! Lődd le őket pontokért."),
            ["tut2"] = ("Grab glowing power-ups. Blue crates unlock new weapons.", "Vedd fel a világító erősítéseket. A kék ládák új fegyvert adnak."),
            ["tut3"] = ("Enter the red barn to save a checkpoint.", "Lépj be a piros pajtába a mentéshez (checkpoint)."),

            // Pickups / events
            ["pickupHealth"] = ("+HEALTH", "+ÉLET"),
            ["pickupDamage"] = ("DOUBLE DAMAGE!", "DUPLA SEBZÉS!"),
            ["pickupRapid"] = ("RAPID FIRE!", "GYORSTÜZELÉS!"),
            ["pickupWeapon"] = ("NEW WEAPON: {0}", "ÚJ FEGYVER: {0}"),
            ["checkpoint"] = ("CHECKPOINT SAVED", "CHECKPOINT MENTVE"),

            // Enemies / kill feed
            ["e_carrot"] = ("Carrot", "Répa"),
            ["e_eggplant"] = ("Eggplant", "Padlizsán"),
            ["e_pickle"] = ("Pickle Spitter", "Köpködő uborka"),
            ["e_pumpkin"] = ("Pumpkin", "Tök"),
            ["e_king"] = ("PUMPKIN KING", "TÖKKIRÁLY"),
            ["kill"] = ("{0} splatted", "{0} szétloccsantva"),
            ["bossKilled"] = ("BOSS DEFEATED!", "FŐELLENSÉG LEGYŐZVE!"),
            ["hugeWave"] = ("A huge wave of mutants is approaching!", "Hatalmas mutáns hullám közeleg!"),

            // Weapons
            ["w_rifle"] = ("Farm Rifle", "Farmer puska"),
            ["w_shotgun"] = ("Shotgun", "Sörétes"),
            ["w_sprayer"] = ("Weed Sprayer", "Gyomirtó permetező"),
            ["w_launcher"] = ("Seed Launcher", "Magvető ágyú"),

            // Pause / end screens
            ["paused"] = ("PAUSED", "SZÜNET"),
            ["resume"] = ("Resume", "Folytatás"),
            ["restart"] = ("Restart round", "Kör újrakezdése"),
            ["loadCheckpoint"] = ("Load checkpoint", "Checkpoint betöltése"),
            ["mainMenu"] = ("Main menu", "Főmenü"),
            ["dead"] = ("THE VEGGIES ATE\nYOUR BRAINS!", "A ZÖLDSÉGEK MEGETTÉK\nAZ AGYADAT!"),
            ["won"] = ("GARDEN SAVED!", "A KERT MEGMENEKÜLT!"),
            ["playAgain"] = ("Play again", "Új kör"),
            ["wonTag"] = ("(WON)", "(NYERT)"),
            ["newHighScore"] = ("NEW HIGH SCORE!", "ÚJ REKORD!"),
            ["stats"] = ("Score: {0}    Wave: {1}    Kills: {2}\nAccuracy: {3}%    Best combo: x{4}    Time: {5}",
                         "Pont: {0}    Hullám: {1}    Ölések: {2}\nPontosság: {3}%    Legjobb kombó: x{4}    Idő: {5}"),
        };

        public static string T(string key)
        {
            if (!table.TryGetValue(key, out var v)) return key;
            return Current == Language.Hungarian ? v.hu : v.en;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);
    }
}
