using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Menu.Remix.MixedUI;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using System;
using RWCustom;
using DevConsole;
using DevConsole.Commands;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using MoreSlugcats;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]


namespace LightsOut
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class LightsOutMain : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "yeliah.lightsout";
        public const string PLUGIN_NAME = "Lights Out!";
        public const string PLUGIN_VERSION = "1.2.1";

        private OptionsMenu optionsMenuInstance;
        private bool initialized;
        private bool lightsOff;
        private int lightsOffCounter;

        //stuff i yoinked from slimecubed :)
        private static readonly Regex entityID = new Regex(@"^ID\.-?\d+\.-?\d+(\.-?\d+)?$");
        private static EntityID ParseExtendedID(string id)
        {
            EntityID outID = EntityID.FromString(id);
            string[] split = id.Split('.');
            if (split.Length > 3 && int.TryParse(split[3], out int altSeed))
            {
                outID.setAltSeed(altSeed);
            }
            return outID;
        }
        private void OnEnable()
        {
            On.RainWorld.OnModsInit += this.RainWorld_OnModsInit;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            if (this.initialized)
            {
                return;
            }
            this.initialized = true;
            this.optionsMenuInstance = new OptionsMenu(this);
            try
            {
                global::MachineConnector.SetRegisteredOI("yeliah.lightsout", optionsMenuInstance);
            }
            catch (Exception ex)
            {
                Debug.Log(string.Format("yeliah.lightsout: OnModsInit options failed init error {0}{1}", this.optionsMenuInstance, ex));
                base.Logger.LogError(ex);
            }
            On.LightSource.HardSetAlpha += LightHardSetAlphaHook;
            On.FlareBomb.DrawSprites += flareBombDrawSpritesHook;
            On.Lantern.DrawSprites += lanternDrawSpritesHook;
            IL.LightSource.Update += lightUpdateHook;
            On.RainWorldGame.Update += updateHook;
            On.ScavengerGraphics.DrawSprites += ScavengerGraphicsDrawSpritesHook;
            On.OverseerGraphics.SafariCursor.DrawSprites += safariCursorDrawHook;
            On.MoreSlugcats.AncientBot.DrawSprites += ancientBotDrawHook;
            On.PlayerGraphics.DrawSprites += playerDrawHook;
            try { RegisterCommands(); }
            catch { }
        }

        private void RegisterCommands()
        {
            //throw new NotImplementedException();
            new CommandBuilder("PopulateWithScavs")
            
            .RunGame((game, args) =>   // Uses the RunGame method to specify that it may only run while in-game
            {
                if(game.GetArenaGameSession != null)
                {
                    if(args.Length == 0)
                    {
                        Console.WriteLine("Specify type!!");
                        return;
                    }
                    Room room = game.GetArenaGameSession.room;
                    EntityID id = room.world.game.GetNewID();
                    CreatureTemplate.Type type = CreatureTemplate.Type.Scavenger;
                    if (args[0].Equals("ScavengerElite"))
                        type = (MoreSlugcatsEnums.CreatureTemplateType.ScavengerElite);
                    else if (args[0].Equals("ScavengerKing"))
                        type = (MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing);

                    if (args.Length > 1 && entityID.IsMatch(args[1]))
                    {
                        try
                        {
                            id = ParseExtendedID(args[1]);
                        }
                        catch { }
                    }
                   
                    WorldCoordinate[] coords = new WorldCoordinate[]{
                        new WorldCoordinate(room.abstractRoom.index, 14, 30,-1),
                        new WorldCoordinate(room.abstractRoom.index, 26, 30, -1),
                        new WorldCoordinate(room.abstractRoom.index, 38, 30, -1),
                        new WorldCoordinate(room.abstractRoom.index, 51, 30,-1),
                        new WorldCoordinate(room.abstractRoom.index, 63, 30, -1),
                        new WorldCoordinate(room.abstractRoom.index, 14, 22,-1),
                        new WorldCoordinate(room.abstractRoom.index, 26, 22, -1),
                        new WorldCoordinate(room.abstractRoom.index, 38, 22, -1),
                        new WorldCoordinate(room.abstractRoom.index, 51, 22,-1),
                        new WorldCoordinate(room.abstractRoom.index, 63, 22, -1),
                        new WorldCoordinate(room.abstractRoom.index, 38, 13, -1),
                        new WorldCoordinate(room.abstractRoom.index, 51, 13,-1),
                        new WorldCoordinate(room.abstractRoom.index, 63, 13, -1),
                        new WorldCoordinate(room.abstractRoom.index, 38, 5, -1),
                        new WorldCoordinate(room.abstractRoom.index, 51, 5,-1),
                        new WorldCoordinate(room.abstractRoom.index, 63, 5, -1),
                    };
                    int seed = 0;

                    for (int i = 0; i < coords.Length; i++)
                    {
                        WorldCoordinate coord = coords[i];
                        seed = id.number + i;
                        EntityID incID = new EntityID(id.spawner, seed);
                        AbstractCreature abstractCreature = new AbstractCreature(game.world, StaticWorld.GetCreatureTemplate(type), null, coord, incID);
                        abstractCreature.creatureTemplate.doesNotUseDens = true;

                        room.abstractRoom.AddEntity(abstractCreature);
                        abstractCreature.RealizeInRoom();
                    }
                    game.nextIssuedId = seed;
                }
                else
                    Console.WriteLine("Must be used in arena!!!");
            })
            .AutoComplete(args =>   // Use the delegate version of autocomplete
            {
                // Argument 0: scav type
                if (args.Length == 0) return new string[] { "Scavenger", "ScavengerKing", "ScavengerElite" };

                // Argument 1: id
                if (args.Length == 1) return new string[] { "ID.-1."};

                return null;
            })
            .Help("echo [Scavenger] [ID]")
            .Register();
        }

        private void playerDrawHook(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            sLeaser.sprites[11].isVisible = !lightsOff;
            sLeaser.sprites[10].isVisible = !lightsOff;

        }

        private void ancientBotDrawHook(On.MoreSlugcats.AncientBot.orig_DrawSprites orig, MoreSlugcats.AncientBot self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            for (int j = self.LightIndex; j < self.AfterLightIndex; j++)
            {
                sLeaser.sprites[j].isVisible = !lightsOff;
            }
        }

        private void safariCursorDrawHook(On.OverseerGraphics.SafariCursor.orig_DrawSprites orig, OverseerGraphics.SafariCursor self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].isVisible = !lightsOff;
            }
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if(lightsOff)
            {
                sLeaser.sprites[self.firstSprite + 8].isVisible = false;
                sLeaser.sprites[self.firstSprite + 9].isVisible = false;

            }

        }

        private void ScavengerGraphicsDrawSpritesHook(On.ScavengerGraphics.orig_DrawSprites orig, ScavengerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
      
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (OptionsMenu.hideMasks.Value && ModManager.MSC && self.maskGfx != null && !self.scavenger.dead && !self.scavenger.readyToReleaseMask)
            {
                self.maskGfx.SetVisible(sLeaser, !lightsOff);
            }
            /*if (self.scavenger.King)
            {
                sLeaser.sprites[self.totalSprites - 1].isVisible = !lightsOff;
                sLeaser.sprites[self.totalSprites - 2].isVisible = !lightsOff;
                if (lightsOff)
                {
                    sLeaser.sprites[self.totalSprites - 1].alpha = 0;
                    sLeaser.sprites[self.totalSprites - 2].alpha = 0;
                }

            }*/
        }

        private void LightHardSetAlphaHook(On.LightSource.orig_HardSetAlpha orig, LightSource self, float a)
        {
            if (lightsOff)
                a = 0;
            orig(self, a);
        }

        private void flareBombDrawSpritesHook(On.FlareBomb.orig_DrawSprites orig, FlareBomb self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            sLeaser.sprites[2].isVisible = !lightsOff;
            orig(self, sLeaser, rCam, timeStacker, camPos);
        }

        private void lanternDrawSpritesHook(On.Lantern.orig_DrawSprites orig, Lantern self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            sLeaser.sprites[2].isVisible = !lightsOff;
            sLeaser.sprites[3].isVisible = !lightsOff;
            orig(self, sLeaser, rCam, timeStacker, camPos);
        }

        private void updateHook(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            orig(self);
            isSwitchPressed();
            if(lightsOffCounter==1)
            {
                //for testing purposes
                //self.GetArenaGameSession.room.AddObject(mouse);

                lightsOff = !lightsOff;
            }
            //Debug.Log("LIGHT STATE IS: " + lightsOff);
        }

        private void lightUpdateHook(ILContext il)
        {
            try
            {
                ILCursor c = new ILCursor(il);
                Func<Instruction, bool>[] array = new Func<Instruction, bool>[3];
                array[0] = ((Instruction i) => i.MatchLdarg(0));
                array[1] = ((Instruction i) => i.MatchLdarg(0));
                array[2] = ((Instruction i) => i.MatchLdflda<LightSource>("setRad"));
                c.GotoNext(MoveType.After, array);
                c.Index++;
                c.EmitDelegate<Func<float, float>>((val) =>
                {
                    if (lightsOff)
                    {
                        return 0;
                    }
                    return val;
                });
                Func<Instruction, bool>[] array2 = new Func<Instruction, bool>[3];
                array2[0] = ((Instruction i) => i.MatchLdarg(0));
                array2[1] = ((Instruction i) => i.MatchLdarg(0));
                array2[2] = ((Instruction i) => i.MatchLdflda<LightSource>("setAlpha"));
                c.GotoNext(MoveType.After, array2);
                c.Index++;
                c.EmitDelegate<Func<float, float>>((val) =>
                {
                    if (lightsOff)
                    {
                        return 0;
                    }
                    return val;
                });

            }
            catch (Exception e)
            {
                base.Logger.LogError("lightUpdateHook encountered an error: " + e);
                throw;
            }
        }
        bool isSwitchPressed()
        {
            bool isPressed = false;
            if (Input.GetKey(OptionsMenu.lightToggle.Value))
                lightsOffCounter++;
            else
                lightsOffCounter = 0;
            return isPressed;
        }
    }
    public class OptionsMenu : OptionInterface
    {
        public OptionsMenu(LightsOutMain plugin)
        {
            lightToggle = this.config.Bind<KeyCode>("lightsoutLightToggle", KeyCode.H);
            hideMasks = this.config.Bind<bool>("lightsoutHideMasks", false);

        }
        public override void Initialize()
        {
            base.Initialize();
            float TITLE_HORIZONTAL = 40f;
            float VERT_POS_INIT = 550f;
            float vert_pos = VERT_POS_INIT;
            float KEYBIND_HORIZONTAL_OFFSET = 200f;
            float KEYBIND_VERT_OFFSET = -40f;
            float KEYBIND_HEIGHT = (KEYBIND_VERT_OFFSET * (-1)) - 20f;
            float KEYBIND_LENGTH = KEYBIND_HORIZONTAL_OFFSET - 50f;
            float CHECKBOX_HORIZONTAL = 60f;
            float CHECKBOX_LABEL_HORIZONTAL = CHECKBOX_HORIZONTAL + 35f;
            float CHECKBOX_VERT_OFFSET = 70f;


            OpTab opTab = new OpTab(this, "Config");
            this.Tabs = new OpTab[]
            {
                opTab
            };
            OpContainer tab1Container = new OpContainer(new Vector2(0, 0));
            opTab.AddItems(tab1Container);
            UIelement[] element = new UIelement[]
            {
                 new OpLabel(TITLE_HORIZONTAL, vert_pos, "Lights Out Keybind", true),
                 new OpKeyBinder(lightToggle,new Vector2(TITLE_HORIZONTAL, vert_pos += KEYBIND_VERT_OFFSET),new Vector2(KEYBIND_LENGTH,KEYBIND_HEIGHT),false, OpKeyBinder.BindController.AnyController),
                 new OpCheckBox(hideMasks, CHECKBOX_HORIZONTAL, vert_pos -= CHECKBOX_VERT_OFFSET),
                 new OpLabel(CHECKBOX_LABEL_HORIZONTAL, vert_pos, "Hide Elite Scav Masks When Lights Out?", false),
            };
            opTab.AddItems(element);

        }
        public static Configurable<KeyCode> lightToggle;
        public static Configurable<bool> hideMasks;
    }

}