﻿// --------------------
// GameApp.cs
// Elias Watson, Isaac Abrahamson, David Polar
// Main Model Class
// --------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Urho;
using Urho.Urho2D;
using System.Timers;
using System.Globalization;
using Battle_Platformer_Xamarin.Model;
using Urho.Audio;
using Urho.Gui;
using Battle_Platformer_Xamarin;

namespace Royale_Platformer.Model
{
    // GameApp
    // Control an UrhoSharp game
    public class GameApp : Application
    {
        // Singleton instance of GameApp
        public static GameApp Instance { get; private set; }

        // Holds a reference to the PlayerCharacter class
        public CharacterPlayer PlayerCharacter { get; private set; }

        // List of all the characters in the game
        public List<Character> Characters { get; private set; }

        // List of all the pickups in the game
        public List<Pickup> Pickups { get; set; }

        // List of bulllets currently in the world
        public List<Bullet> Bullets { get; set; }

        // List of MapTiles
        public List<MapTile> Tiles { get; set; }

        // List of collidable objects in the world
        private List<WorldObject> collisionObjects;

        // Used to call restart functionality on Xamarin Forms page
        public Func<object> Restart { get; internal set; }

        // Used to call win functionality on Xamarin Forms page
        public Func<object> HandleWin { get; internal set; }

        // Used to call lose functionality on Xamarin Forms page
        public Func<object> HandleLose { get; internal set; }

        // Speed of bullets
        private static readonly float bulletSpeed = 10f;

        // List of possible enemy classes
        private static readonly List<CharacterClass> enemyClasses = new List<CharacterClass> {
            CharacterClass.Gunner, CharacterClass.Support, CharacterClass.Tank
        };

        // Urho scene
        private Scene scene;

        // Urho camera node
        private Node cameraNode;

        // Holds the parent element for all the HUD items
        private UIElement hud;

        // Holds the time remaining in the game
        private int time;

        // Stores the difficulty of the game
        public bool hardcore;

        // Determines if the game has been loaded from a saved game
        private bool continueGame;

        // Stores CharacterClass of PlayerCharacter
        private CharacterClass charClass;

        // Used to count down the seconds remaining in game
        Timer timer;

        // Percentage of possible weapon spawns to spawn
        private float weaponSpawnRate = 0.2f;

        // Percentage of possible armor spawns to spawn
        private float armorSpawnRate = 0.2f;

        // Number of enemies to spawn
        private int enemyCount = 5;

        // List of possible player spawns
        private List<Vector2> playerSpawns;

        // List of possible enemy spawns
        private List<Vector2> enemySpawns;

        // tracks if the game has been won/lost
        private bool gameover = false;

        // determins if the game is in Schaub Mode
        public bool schaubMode = false;

        // List of enemy sprite references
        public List<Sprite2D> EnemySprites = new List<Sprite2D>();

        // List of alternative enemy sprite references
        public List<Sprite2D> EnemySprites2 = new List<Sprite2D>();

        // Urho2D tile map
        private TileMap2D tileMap;

        // Create the game app
        // <options> Urho options
        public GameApp(ApplicationOptions options) : base(options)
        {
            Instance = this;
            string[] flags = options.AdditionalFlags.ToString().Split(',');

            hardcore = flags[0] == "True" ? true : false;
            continueGame = flags[1] == "True" ? true : false;
            schaubMode = flags[3] == "True" ? true : false;

            if (hardcore)
            {
                weaponSpawnRate = 0.1f;
                armorSpawnRate = 0.1f;
                enemyCount = 10;
            }

            switch (flags[2])
            {
                case "Gunner":
                    charClass = CharacterClass.Gunner;
                    break;
                case "Tank":
                    charClass = CharacterClass.Tank;
                    break;
                case "Support":
                    charClass = CharacterClass.Support;
                    break;
                case "Schaub":
                    charClass = CharacterClass.Schaub;
                    break;
            }

            Characters = new List<Character>();
            Pickups = new List<Pickup>();
            Bullets = new List<Bullet>();
            Tiles = new List<MapTile>();
            collisionObjects = new List<WorldObject>();
        }

        // Start the game
        // Called by Urho
        protected override void Start()
        {
            base.Start();

            float halfWidth = Graphics.Width * 0.5f * PixelSize;
            float halfHeight = Graphics.Height * 0.5f * PixelSize;

            // Create Scene
            scene = new Scene();
            scene.CreateComponent<Octree>();
            //scene.CreateComponent<PhysicsWorld2D>();

            playerSpawns = new List<Vector2>();
            enemySpawns = new List<Vector2>();

            InvokeOnMain(() =>
            {
                CreateMap();

                Vector2 playerSpawn = playerSpawns.GetRandomElement();
                playerSpawn += new Vector2(0f, 0.2f);

                cameraNode = scene.CreateChild("Camera");
                cameraNode.Position = new Vector3(playerSpawn.X, playerSpawn.Y, -1);

                Camera camera = cameraNode.CreateComponent<Camera>();
                camera.Orthographic = true;
                camera.OrthoSize = 2 * halfHeight;
                camera.Zoom = Math.Min(Graphics.Width / 1920.0f, Graphics.Height / 1080.0f);

                // Create BG
                {
                    Sprite2D bgSprite = ResourceCache.GetSprite2D("test/bg.png");
                    if (bgSprite == null)
                        throw new Exception("Bacgkround not found");

                    Node bgNode = scene.CreateChild();
                    bgNode.Position = new Vector3(25f, 12.5f, 100f);
                    bgNode.SetScale(5000f / 1024f);

                    StaticSprite2D bgStaticSprite = bgNode.CreateComponent<StaticSprite2D>();
                    bgStaticSprite.Sprite = bgSprite;
                }

                if (!continueGame)
                    time = hardcore ? 1500 : 3000;

                if (!continueGame && !schaubMode) CreatePlayer(playerSpawn.X, playerSpawn.Y);
                if (schaubMode) LoadSchaub();
                if (!continueGame) CreateEnemies();

                CreateHUD();
                CreateClock();

                /*
                Bullets.Add(new Bullet(1, scene, bulletSprite, new Vector2(4, -2)));
                */

                // Setup Viewport
                Renderer.SetViewport(0, new Viewport(Context, scene, camera, null));
            });
        }

        // This method starts the game for Schaub Mode
        public void LoadSchaub()
        {
            schaubMode = true;
            charClass = CharacterClass.Schaub;
            // Create default player with correct class
            CreatePlayer(25, 20);

            // Update Player
            PlayerCharacter.MaxHealth = 100;
            PlayerCharacter.Position = new Vector3(0, 0, -1);
            PlayerCharacter.Health = 100;
            PlayerCharacter.HeldWeapon = new WeaponSchaub();
            PlayerCharacter.Armor = true;
            PlayerCharacter.Score = 1000;
            PlayerCharacter.WorldNode.SetScale(0.5f);

            // Update Camera
            cameraNode.Parent = PlayerCharacter.WorldNode;
            cameraNode.Position = new Vector3(0, 0, -1);
        }

        #region Gameplay Methods

        // This method plays a sound effect
        // Takes a filename and condition whether or not to loop
        private void PlaySound(string name, bool looped)
        {
            Sound sound = new Sound();
            //sound.Looped = looped;
            sound.LoadOggVorbis(ResourceCache.GetFile(name));

            Node soundNode = scene.CreateChild("Sound");
            SoundSource soundSource = soundNode.CreateComponent<SoundSource>();
            soundSource.AutoRemoveMode = AutoRemoveMode.Node;
            soundSource.Play(sound);
            //source.AutoRemoveMode = AutoRemoveMode.Component;
        }

        // Create the player object
        // <x> world x position
        // <y> world y position
        private void CreatePlayer(float x, float y)
        {
            Sprite2D shieldSprite = ResourceCache.GetSprite2D("shield.png");
            CharacterPlayer player = new CharacterPlayer(charClass, 10);

            if (schaubMode)
            {
                player.PlayerImage1 = ResourceCache.GetSprite2D("characters/cheat.png");
                player.PlayerSpriteAttack = player.PlayerImage1;
                player.CreateNode(scene, player.PlayerSpriteAttack, shieldSprite, new Vector2(x, y));
                Characters.Add(player);

                cameraNode.Parent = player.WorldNode;
                PlayerCharacter = player;
                return;
            }

            player.CreateNode(scene, player.PlayerSpriteAttack, shieldSprite, new Vector2(x, y));
            PlayerCharacter = player;
            AddPlayer(player);
        }

        // Spawn the enemies
        private void CreateEnemies()
        {
            Sprite2D shieldSprite = ResourceCache.GetSprite2D("shield.png");
            for (int i = 0; i < enemyCount; ++i)
            {
                CharacterEnemy enemy = new CharacterEnemy(enemyClasses.GetRandomElement(), 5);

                Vector2 spawn = enemySpawns.GetRandomElement();
                enemySpawns.Remove(spawn);

                enemy.CreateNode(scene, enemy.PlayerSpriteAttack, shieldSprite, spawn);
                Characters.Add(enemy);
            }
        }

        // Load the map
        private void CreateMap()
        {
            // Load map
            TmxFile2D mapFile = ResourceCache.GetTmxFile2D("test/map_1.tmx");
            if (mapFile == null)
                throw new Exception("Map not found");

            Node mapNode = scene.CreateChild("TileMap");
            mapNode.Position = new Vector3(0f, 0f, 10f);
            mapNode.SetScale(1f / 0.7f);

            tileMap = mapNode.CreateComponent<TileMap2D>();
            tileMap.TmxFile = mapFile;

            // Load pickup sprites
            var weaponSprite = ResourceCache.GetSprite2D("map/levels/platformer-art-complete-pack-0/Request pack/Tiles/raygunBig.png");
            var armorSprite = ResourceCache.GetSprite2D("map/levels/platformer-art-complete-pack-0/Request pack/Tiles/shieldGold.png");

            if (weaponSprite == null || armorSprite == null)
                throw new Exception("Texture not found");

            // Initialize map
            Random r = new Random();
            for (uint layerID = 0; layerID < tileMap.NumLayers; ++layerID)
            {
                TileMapLayer2D layer = tileMap.GetLayer(layerID);
                for (int x = 0; x < layer.Width; ++x)
                {
                    for (int y = 0; y < layer.Height; ++y)
                    {
                        Node n = layer.GetTileNode(x, y);
                        if (n == null) continue;

                        Vector2 pos = tileMap.TileIndexToPosition(x, y);
                        pos /= 0.7f;
                        pos += new Vector2(0.5f, 0.5f);

                        // Solid Block
                        if (layer.GetProperty("Solid") == "True")
                        {
                            MapTile tile = new MapTile(pos);
                            Tiles.Add(tile);
                            collisionObjects.Add(tile);
                            continue;
                        }

                        // Spawner
                        switch (layer.GetProperty("Spawn"))
                        {
                            case "Player":
                                playerSpawns.Add(pos);
                                break;

                            case "Enemy":
                                enemySpawns.Add(pos);
                                break;

                            case "Weapon":
                                if (r.NextDouble() < weaponSpawnRate)
                                    Pickups.Add(new PickupWeaponUpgrade(scene, weaponSprite, pos));
                                break;

                            case "Armor":
                                if (r.NextDouble() < armorSpawnRate)
                                    Pickups.Add(new PickupArmor(scene, armorSprite, pos));
                                break;

                            default:
                                continue;
                        }

                        // Skipped if not a spawner
                        n.Remove();
                    }
                }
            }
        }

        // Update the game state
        // Called every frame
        // <timeStep> time in seconds since last frame
        protected async override void OnUpdate(float timeStep)
        {
            base.OnUpdate(timeStep);

            // Shield
            foreach (Character c in Characters)
                c.ShieldNode.Position = c.ShieldUp ? c.WorldNode.Position : new Vector3(1000, 10000, -1000);

            // Pickups
            foreach (Character c in Characters)
            {
                foreach (Pickup p in Pickups.ToList())
                {
                    if (c.Collides(p))
                    {
                        if (p.PickUp(c))
                        {
                            if (c is CharacterPlayer)
                                PlaySound("sounds/effects/pop.ogg", false);
                            p.WorldNode.Remove();
                            Pickups.Remove(p);

                            c.WorldNode.GetComponent<StaticSprite2D>().Sprite = c.PlayerSpriteAttack;
                        }
                    }
                }
            }

            // Bullets
            foreach (Bullet b in Bullets.ToList())
            {
                if (b.WorldNode.IsDeleted) continue;

                if (b.WorldNode.Position2D.Length > 50f)
                {
                    b.WorldNode.Remove();
                    Bullets.Remove(b);
                    continue;
                }

                bool deleted = false;
                b.WorldNode.SetPosition2D(b.WorldNode.Position2D + (b.Direction * bulletSpeed * timeStep));

                foreach (Character c in Characters)
                {
                    if (b.Owner == c) continue;
                    if (c.Collides(b))
                    {
                        c.Hit(b);
                        b.WorldNode.Remove();
                        Bullets.Remove(b);
                        deleted = true;
                        break;
                    }
                }

                if (deleted) continue;

                foreach (WorldObject o in collisionObjects)
                {
                    if (b.WorldNode.IsDeleted || o.WorldNode.IsDeleted)
                        continue;

                    if (o.Collides(b))
                    {
                        b.WorldNode.Remove();
                        Bullets.Remove(b);
                        break;
                    }
                }
            }

            // PlayerCharacter may have to be loaded from saved game
            if (PlayerCharacter != null)
            {
                PlayerCharacter.Input.W = Input.GetKeyDown(Key.W);
                PlayerCharacter.Input.A = Input.GetKeyDown(Key.A);
                PlayerCharacter.Input.S = Input.GetKeyDown(Key.S);
                PlayerCharacter.Input.D = Input.GetKeyDown(Key.D);
                PlayerCharacter.Input.F = Input.GetKeyDown(Key.F);
                PlayerCharacter.Input.Space = Input.GetKeyPress(Key.Space);
                PlayerCharacter.Input.LeftClick = Input.GetKeyDown(Key.E);


                Vector2 mousePosition = new Vector2(Input.MousePosition.X, Input.MousePosition.Y);
                Vector2 resolution = new Vector2(Graphics.Width, Graphics.Height);
                Vector2 mouseUV = ((2f * mousePosition) - resolution) / resolution.Y;
                mouseUV.Y *= -1f;
                PlayerCharacter.Input.MousePosition = mouseUV;

                foreach (Character c in Characters.ToList())
                {
                    // Death
                    if (c.Health <= 0)
                    {
                        // sound effect
                        // create new node to play sound from, as character will be removed
                        var node = new Scene().CreateChild();
                        node.Position = c.Position;
                        PlaySound("sounds/effects/death.ogg", false);

                        c.WorldNode.Remove();
                        Characters.Remove(c);

                        if (c.LastBullet.Owner is CharacterPlayer)
                            if (hardcore)
                                PlayerCharacter.Score += 150;
                            else
                                PlayerCharacter.Score += 100;

                        if (Characters.Count == 1 && Characters.First() == PlayerCharacter)
                        {
                            gameover = true;
                            timer.Enabled = false;
                            HandleWin();
                        }
                        else if (c == PlayerCharacter)
                        {
                            gameover = true;
                            timer.Enabled = false;
                            HandleLose();
                        }

                        continue;
                    }

                    c.UpdateCollision(collisionObjects);
                    c.Update(timeStep);
                }

                PlayerCharacter.Input.LeftClick = false;

                if (Input.GetKeyDown(Key.F1) && !schaubMode)
                {
                    Save("latest.txt");
                    var saved = new Text() { Value = "Game Saved" };

                    saved.SetColor(Color.Cyan);
                    saved.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);
                    saved.VerticalAlignment = VerticalAlignment.Center;
                    saved.HorizontalAlignment = HorizontalAlignment.Center;

                    InvokeOnMain(() => { UI.Root.AddChild(saved); });
                    await Task.Delay(500);
                    try
                    {
                        InvokeOnMain(() =>
                        {
                            try { UI.Root.RemoveChild(saved); }
                            catch { return; }
                        });
                    }
                    catch { return; }
                }

                if (Input.GetKeyDown(Key.F2))
                {
                    timer.Enabled = false;
                    Restart();
                }
            }
        }

        // Add the player's character to the game
        public void AddPlayer(CharacterPlayer character)
        {
            PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage1;
            Characters.Add(character);

            cameraNode.Parent = character.WorldNode;
        }

        // This method creates the Heads Up Display
        private void CreateHUD()
        {
            hud = new UIElement()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                LayoutMode = LayoutMode.Vertical,
                LayoutSpacing = 5
            };

            if (PlayerCharacter != null) UpdateHUD();
            UI.Root.AddChild(hud);
        }

        // This method creates a new timer for the game
        private void CreateClock()
        {
            timer = new Timer(100);
            timer.Elapsed += GameTick;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        // This method is the timer event handler and decrements the time every 1/10 second
        private void GameTick(Object source, ElapsedEventArgs e)
        {
            --time;

            if (time <= 0)
            {
                gameover = true;
                HandleLose();
                timer.Enabled = false;
                return;
            }

            UpdateHUD();
        }
        
        // This method is called every tick and updates the HUD values
        private void UpdateHUD()
        {
            if (gameover) return;

            InvokeOnMain(() =>
            {
                hud.RemoveAllChildren();

                var difficulty = new Text() { Value = hardcore ? "Difficulty: Hardcore" : "Difficulty: Normal" };
                var weapon = new Text() { Value = $"Weapon: {PlayerCharacter.HeldWeapon.Serialize()}" };
                var armor = new Text() { Value = PlayerCharacter.Armor ? "Armor: Protected" : "Armor: Missing" };
                var health = new Text() { Value = $"Health: {PlayerCharacter.Health.ToString()}" };
                var clock = new Text() { Value = $"Time: {TimeSpan.FromSeconds(time / 10).ToString(@"mm\:ss")}" };
                var score = new Text() { Value = $"Score: {PlayerCharacter.Score.ToString()}" };

                difficulty.SetColor(Color.Red);
                weapon.SetColor(Color.Red);
                armor.SetColor(Color.Red);
                health.SetColor(Color.Red);
                clock.SetColor(Color.Red);
                score.SetColor(Color.Red);

                difficulty.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);
                weapon.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);
                armor.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);
                health.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);
                clock.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);
                score.SetFont(font: ResourceCache.GetFont("fonts/FiraSans-Regular.otf"), size: 15);

                hud.AddChild(difficulty);
                hud.AddChild(weapon);
                hud.AddChild(armor);
                hud.AddChild(health);
                hud.AddChild(clock);
                hud.AddChild(score);
            });
        }

        // Spawn bullets in the world
        // <bullets> list of bullets to spawn
        // <character> character that shot the bullets
        public async void CreateBullets(List<Bullet> bullets, Character character)
        {
            InvokeOnMain(async () =>
            {
                // handle knife
                if (bullets == null)
                {
                    Bullet b = new Bullet(20) { Owner = character };
                    b.CreateNode(scene, ResourceCache.GetSprite2D("knife.png"), character.WorldNode.Position2D);
                    Bullets.Add(b);

                    var node = new Scene().CreateChild();
                    node.Position = b.WorldNode.Position;
                    if (character is CharacterPlayer)
                        PlaySound("sounds/effects/jump.ogg", false);

                    await Task.Delay(200);
                    if (!b.WorldNode.IsDeleted && Bullets.Contains(b))
                    {
                        try { Bullets.Remove(b); }
                        catch { return; }
                    }

                    // if bullet collides with a player, it will be already removed from the world,
                    // so ignore error if thrown.
                    try { b.WorldNode.Remove(); }
                    catch { return; }

                    return;
                }

                bool playedSound = false;
                foreach (Bullet b in bullets)
                {
                    b.Owner = character;
                    Sprite2D bulletSprite;

                    switch (b.Owner.Class)
                    {
                        case CharacterClass.Support:
                            bulletSprite = ResourceCache.GetSprite2D("shell.png");
                            break;
                        case CharacterClass.Schaub:
                            bulletSprite = ResourceCache.GetSprite2D("cheatShot.png");
                            break;
                        default:
                            bulletSprite = ResourceCache.GetSprite2D("shot.png");
                            break;
                    }

                    b.CreateNode(scene, bulletSprite, character.WorldNode.Position2D);

                    Bullets.Add(b);

                    if (schaubMode && b.Owner is CharacterPlayer)
                    {
                        PlaySound("sounds/effects/schaubShot.ogg", false);
                        playedSound = true;
                        continue;
                    }

                    // Don't repeat sound for shotguns
                    if (bullets.Count >= 4 && playedSound) continue;

                    if (bullets.Count >= 4)
                        PlaySound("sounds/effects/shotgun.ogg", false);
                    else
                        PlaySound("sounds/effects/gunshot.ogg", false);

                    playedSound = true;
                }
            });
        }
        #endregion

        #region Save/Load Methods

        // This method converts game data into a string
        public string Serialize()
        {
            string output = "";

            // Add Difficulty
            output += hardcore.ToString();

            // Add Time
            output += Environment.NewLine + time.ToString();

            // Add Player
            output += Environment.NewLine + PlayerCharacter.Serialize();

            // Add enemies
            string characterString = "";
            foreach (var character in Characters.Skip(1)) { characterString += $"{character.Serialize()};"; }
            output += Environment.NewLine + characterString;

            // Add pickups
            string pickupString = "";
            foreach (var item in Pickups) { pickupString += $"{item.Serialize()};"; }
            output += Environment.NewLine + pickupString;

            return output;
        }

        // This method calls specific load functionality for each line of the saved game data
        // Takes a string of data in correct format
        public void Deserialize(string serialized)
        {
            using (StringReader reader = new StringReader(serialized))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    switch (lineNumber)
                    {
                        case 0: // Difficulty
                            hardcore = line == "True" ? true : false;
                            ++lineNumber;
                            break;
                        case 1: // Time
                            time = Convert.ToInt32(line);
                            ++lineNumber;
                            break;
                        case 2: // Player
                            LoadPlayer(line);
                            ++lineNumber;
                            break;
                        case 3: // Enemies
                            LoadEnemies(line);
                            ++lineNumber;
                            break;
                        case 4: // Pickups
                            LoadPickups(line);
                            ++lineNumber;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        // This method loads the player character
        // Recieves a line of serialized player data
        private void LoadPlayer(string line)
        {
            InvokeOnMain(() =>
            {
                // Create variables to store character properties.
                // Order of serialized is:
                // Class,HeldWeapon,Armor,Health,MaxHealth,Score,X,Y,Z
                string[] props = line.Split(',');
                var playerClass = props[0];
                var playerWeapon = props[1];
                var playerArmor = props[2];
                var playerHealth = props[3];
                var playerMaxHealth = props[4];
                var playerScore = props[5];

                string[] position = props.Skip(6).Take(3).ToArray();
                float x = float.Parse(position[0], CultureInfo.InvariantCulture.NumberFormat);
                float y = float.Parse(position[1], CultureInfo.InvariantCulture.NumberFormat);
                float z = float.Parse(position[2], CultureInfo.InvariantCulture.NumberFormat);

                // Determine CharacterClass
                switch (playerClass)
                {
                    case "Gunner":
                        charClass = CharacterClass.Gunner;
                        break;
                    case "Support":
                        charClass = CharacterClass.Support;
                        break;
                    case "Tank":
                        charClass = CharacterClass.Tank;
                        break;
                }

                // Create default player with correct class
                CreatePlayer(x, y);

                // Determine HeldWeapon
                Weapon heldWeapon = new WeaponKnife();
                switch (playerWeapon)
                {
                    case "Royale_Platformer.Model.WeaponKnife":
                        heldWeapon = new WeaponKnife();
                        break;
                    case "Royale_Platformer.Model.WeaponPistol":
                        heldWeapon = new WeaponPistol();
                        PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage2;
                        break;
                    case "Royale_Platformer.Model.WeaponPistolShield":
                        heldWeapon = new WeaponPistolShield();
                        PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage2;
                        break;
                    case "Royale_Platformer.Model.WeaponShotgun":
                        heldWeapon = new WeaponShotgun();
                        PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage2;
                        break;
                    case "Royale_Platformer.Model.WeaponAdvancedShotgun":
                        heldWeapon = new WeaponAdvancedShotgun();
                        PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage2;
                        break;
                    case "Royale_Platformer.Model.WeaponAR":
                        heldWeapon = new WeaponAR();
                        PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage2;
                        break;
                    case "Royale_Platformer.Model.WeaponAdvancedAR":
                        heldWeapon = new WeaponAdvancedAR();
                        PlayerCharacter.PlayerSpriteAttack = PlayerCharacter.PlayerImage2;
                        break;
                }

                // Update Player
                PlayerCharacter.MaxHealth = Convert.ToInt32(playerMaxHealth);
                PlayerCharacter.Position = new Vector3(x, y, z);
                PlayerCharacter.Health = Convert.ToInt32(playerHealth);
                PlayerCharacter.HeldWeapon = heldWeapon;
                PlayerCharacter.Armor = playerArmor == "True" ? true : false;
                PlayerCharacter.Score = Convert.ToInt32(playerScore);

                // Update Camera
                cameraNode.Parent = PlayerCharacter.WorldNode;
                cameraNode.Position = new Vector3(x, y, -1);
            });
        }

        // This method loads the pickups
        // Recieves a line of serialized pickup data
        private void LoadPickups(string line)
        {
            InvokeOnMain(() =>
            {
                // Create images
                var weaponSprite = ResourceCache.GetSprite2D("map/levels/platformer-art-complete-pack-0/Request pack/Tiles/raygunBig.png");
                var armorSprite = ResourceCache.GetSprite2D("map/levels/platformer-art-complete-pack-0/Request pack/Tiles/shieldGold.png");
                if (weaponSprite == null || armorSprite == null)
                    throw new Exception("Texture not found");

                // Clear Tilemap of old pickups
                for (uint layerID = 0; layerID < tileMap.NumLayers; ++layerID)
                {
                    TileMapLayer2D layer = tileMap.GetLayer(layerID);
                    for (int x = 0; x < layer.Width; ++x)
                    {
                        for (int y = 0; y < layer.Height; ++y)
                        {
                            Node n = layer.GetTileNode(x, y);
                            if (n == null) continue;
                            if (layer.HasProperty("Spawn")) n.Remove();
                        }
                    }
                }

                // Remove added pickups from map.
                foreach (Pickup p in Pickups)
                    p.WorldNode.Remove();
                Pickups.Clear();

                // Load each pickup
                string[] pickupsSplit = line.Split(';');
                foreach (var pickup in pickupsSplit.Take(pickupsSplit.Length - 1))
                {
                    Pickup pickupObj;

                    // determine type
                    var pickupType = pickup.Split(',')[0];
                    if (pickupType == "Royale_Platformer.Model.PickupArmor")
                        pickupObj = new PickupArmor();
                    else
                        pickupObj = new PickupWeaponUpgrade();

                    // determine position
                    var position = pickupObj.Deserialize(pickup);

                    // Load
                    if (pickupType == "Royale_Platformer.Model.PickupArmor")
                        Pickups.Add(new PickupArmor(scene, armorSprite, new Vector2(position.X, position.Y)));
                    else
                        Pickups.Add(new PickupWeaponUpgrade(scene, weaponSprite, new Vector2(position.X, position.Y)));
                }
            });
        }

        // This method loads the enemy characters
        // Recieves a line of serialized character data
        private void LoadEnemies(string line)
        {
            InvokeOnMain(() =>
            {
                // Load each enemy
                string[] enemiesSplit = line.Split(';');
                foreach (var enemy in enemiesSplit.Take(enemiesSplit.Length - 1))
                {
                    // Create variables to store character properties.
                    // Order of serialized is:
                    // Class,HeldWeapon,Armor,Health,MaxHealth,Score,X,Y,Z
                    string[] props = enemy.Split(',');
                    var enemyClass = props[0];
                    var enemyWeapon = props[1];
                    var enemyArmor = props[2];
                    var enemyHealth = props[3];
                    var enemyMaxHealth = props[4];
                    var enemyScore = props[5];

                    string[] position = props.Skip(6).Take(3).ToArray();
                    float x = float.Parse(position[0], CultureInfo.InvariantCulture.NumberFormat);
                    float y = float.Parse(position[1], CultureInfo.InvariantCulture.NumberFormat);
                    float z = float.Parse(position[2], CultureInfo.InvariantCulture.NumberFormat);

                    // Determine CharacterClass
                    CharacterClass charClass = CharacterClass.Gunner;
                    switch (enemyClass)
                    {
                        case "Gunner":
                            charClass = CharacterClass.Gunner;
                            break;
                        case "Support":
                            charClass = CharacterClass.Support;
                            break;
                        case "Tank":
                            charClass = CharacterClass.Tank;
                            break;
                    }

                    // Determine HeldWeapon
                    Weapon heldWeapon = new WeaponKnife();
                    switch (enemyWeapon)
                    {
                        case "Royale_Platformer.Model.WeaponKnife":
                            heldWeapon = new WeaponKnife();
                            break;
                        case "Royale_Platformer.Model.WeaponPistol":
                            heldWeapon = new WeaponPistol();
                            break;
                        case "Royale_Platformer.Model.WeaponPistolShield":
                            heldWeapon = new WeaponPistolShield();
                            break;
                        case "Royale_Platformer.Model.WeaponShotgun":
                            heldWeapon = new WeaponShotgun();
                            break;
                        case "Royale_Platformer.Model.WeaponAdvancedShotgun":
                            heldWeapon = new WeaponAdvancedShotgun();
                            break;
                        case "Royale_Platformer.Model.WeaponAR":
                            heldWeapon = new WeaponAR();
                            break;
                    }

                    // Create new Character
                    CharacterEnemy enemyPlayer = new CharacterEnemy(
                        charClass,
                        Convert.ToInt32(enemyMaxHealth),
                        new Vector3(x, y, z)
                    );
                    enemyPlayer.Health = Convert.ToInt32(enemyHealth);
                    enemyPlayer.HeldWeapon = heldWeapon;
                    enemyPlayer.Armor = enemyArmor == "True" ? true : false;
                    enemyPlayer.Score = Convert.ToInt32(enemyScore);

                    Sprite2D shieldSprite = ResourceCache.GetSprite2D("shield.png");

                    // Load Enemy
                    //AnimationSet2D sprite = ResourceCache.GetAnimationSet2D(enemyPlayer.GetSprite());
                    Sprite2D sprite = ResourceCache.GetSprite2D(enemyPlayer.GetSprite());
                    if (sprite == null) throw new Exception("Enemy sprite not found");
                    enemyPlayer.CreateNode(scene, sprite, shieldSprite, new Vector2(enemyPlayer.Position.X, enemyPlayer.Position.Y));
                    Characters.Add(enemyPlayer);
                }
            });
        }

        // This method loads the serialized data from file
        // Takes a filename to load
        public void Load(string fileName)
        {
            if (schaubMode) return;

            string PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), fileName);

            if (File.Exists(PATH))
            {
                string data = "";
                foreach (var line in File.ReadLines(PATH)) { data += line + Environment.NewLine; };
                Deserialize(data);
            }
            else
            {
                throw new Exception("The call could not be completed as dialed. Please check check the number, and try your call again.");
            }
        }

        // This method writes the serialized data to file
        // Takes a filename to write to
        public void Save(string fileName)
        {
            if (schaubMode) return;

            string PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), fileName);

            string serialized = Serialize();
            File.WriteAllText(PATH, serialized);
        }
        #endregion
    }
}
