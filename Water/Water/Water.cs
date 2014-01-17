using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Water.Utils;

namespace Water
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Water : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager _graphics;
        private GraphicsDevice _device;
        private RasterizerState _rasterizerState;
        SpriteBatch _spriteBatch;

        // Text
        private SpriteFont _font;
        private bool _displayInfo = true;

        // Camera
        private Vector3 _cameraPosition;
        private Vector3 _cameraTarget;
        private Vector3 _cameraDirection;

        private float _aspectRatio;
        private float _nearPlane;
        private float _farPlane;
        private float _fieldOfView;

        private float _cameraSpeed;

        private float _cameraPitch; // vertical
        private float _cameraYaw; // horizontal

        // Matrices
        private Matrix _projectionMatrix;
        private Matrix _viewMatrix;

        private Matrix _reflectionViewMatrix;

        // Input
        private MouseState _mouseState;

        // Terrain
        private float[,] _terrainHeights;
        private Point _terrainSize;
        private const float _terrainMaxHeight = 50;
        private Texture2D _terrainTexture;
        private VertexPositionNormalTexture[] _terrainVertices;
        private int[] _terrainIndices;
        private Vector4 _refractionClippingPlane;
        private Vector4 _reflectionClippingPlane;

        // Water
        private float _waterHeight;
        private VertexPositionNormalTexture[] _waterVertices;
        private int[] _waterIndices;

        private bool _enableRenderTarget;

        // Wave normal maps
        private Texture2D _waveNormalMap0;
        private Texture2D _waveNormalMap1;

        // Waves velocity
        private Vector2 _waveVelocity0;
        private Vector2 _waveVelocity1;

        // Wave normal map offsets
        private Vector2 _waveNormalMapOffset0;
        private Vector2 _waveNormalMapOffset1;

        // Refraction
        RenderTarget2D _refractionRenderTarget;
        Texture2D _refractionTexture;

        // Refraction
        RenderTarget2D _reflectionRenderTarget;
        Texture2D _reflectionTexture;

        // Shaders
        private Effect _basicEffect;
        private Effect _refractionEffect;
        private Effect _reflectionEffect;
        private Effect _waterEffect;

        // Lighting
        private bool _enableLighting = false;
        private readonly Vector4 _ambientColor = new Vector4(1, 1, 1, 1);
        private float _ambiantIntensity = 0.75f;
        private Vector3 _diffuseLightDirection = new Vector3(0, 1, 0);
        private readonly Vector4 _diffuseColor = new Vector4(1, 1, 1, 1);
        private float _diffuseIntensity = 1f;

        // Specular lighting
        float _shininess = 200;
        readonly Vector4 _specularColor = new Vector4(1, 1, 1, 1);
        float _specularIntensity = 1;

        // Skyboxes
        private Model _skyboxCube;
        private TextureCube _skyboxTexture;
        private Effect _skyboxEffect;
        private float _skyboxSize = 5000f;

        // Skydome
        Model _skyDome;
        Texture2D _cloudMap;

        // Debug
        private int _renderTargetCounter = 0;

        public Water()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            _device = _graphics.GraphicsDevice;

            _enableRenderTarget = true;

            // Graphics preferences
            _graphics.PreferredBackBufferWidth = 800;
            _graphics.PreferredBackBufferHeight = 600;
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges();
            Window.Title = "Water project";

            // Camera init
            _nearPlane = 1.0f;
            _farPlane = 10000.0f;
            _fieldOfView = 45.0f;

            _cameraPosition = new Vector3(0.0f, 0.0f, 0.0f);
            _cameraDirection = new Vector3(0.0f, 0.0f, 1.0f);
            _cameraTarget = _cameraDirection + _cameraPosition;

            _cameraSpeed = 100.0f;

            _cameraPitch = 0.0f;
            _cameraYaw = 0.0f;

            _aspectRatio = (float)_device.Viewport.Width / _device.Viewport.Height;

            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(_fieldOfView), _aspectRatio, _nearPlane, _farPlane);

            // Waves
            _waveVelocity0 = new Vector2(0.01f, 0.03f) * 10;
            _waveVelocity1 = new Vector2(-0.01f, 0.03f) * 10;

            // Inputs
            Mouse.SetPosition(_device.Viewport.Width / 2, _device.Viewport.Height / 2);
            _mouseState = Mouse.GetState();

            // Components
            Components.Add(new FrameRateCounter(this));
            Components.Add(new InputManager(this));

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            PresentationParameters pp = _device.PresentationParameters;

            // Render targets
            _refractionRenderTarget = new RenderTarget2D(
                _device,
                pp.BackBufferWidth,
                pp.BackBufferHeight,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            _reflectionRenderTarget = new RenderTarget2D(
                _device,
                pp.BackBufferWidth,
                pp.BackBufferHeight,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            CreateRasterizerState(FillMode.Solid);

            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load font file
            _font = Content.Load<SpriteFont>(@"Fonts/ClassicFont");

            // Load textures
            _terrainTexture = Content.Load<Texture2D>(@"Textures/terrain_texture");
            _waveNormalMap0 = Content.Load<Texture2D>(@"Textures/wave0");
            _waveNormalMap1 = Content.Load<Texture2D>(@"Textures/wave1");

            // Load height map
            LoadHeightData(Content.RootDirectory + "/terrain_height.raw");

            // Water
            _waterHeight = 20;
            _refractionClippingPlane = CreateClippingPlane(false);
            _reflectionClippingPlane = CreateClippingPlane(true);

            // Create vertex/index buffers
            SetUpVertices();
            SetUpIndices();

            // Shaders
            _basicEffect = Content.Load<Effect>("Shaders/Lights");
            _refractionEffect = Content.Load<Effect>("Shaders/Refraction");
            _reflectionEffect = Content.Load<Effect>("Shaders/Reflection");
            _waterEffect = Content.Load<Effect>("Shaders/Water");

            // Skyboxes
            _skyboxCube = Content.Load<Model>("Skyboxes/Cube");
            _skyboxTexture = Content.Load<TextureCube>("Skyboxes/Islands");
            _skyboxEffect = Content.Load<Effect>("Shaders/Skybox");

            // Skydome
            _skyDome = Content.Load<Model>("Skyboxes/Dome");
            _skyDome.Meshes[0].MeshParts[0].Effect = _basicEffect.Clone();
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (!IsActive)
                return;

            MouseState mouse = Mouse.GetState();

            // Allows the game to exit
            if (InputManager.KeyDown(Keys.Escape))
                Exit();

            #region Configuration region
            // Switch fill mode
            if (InputManager.KeyPressed(Keys.F1))
            {
                var newFillMode = (_rasterizerState.FillMode == FillMode.Solid)
                    ? FillMode.WireFrame
                    : FillMode.Solid;

                CreateRasterizerState(newFillMode);
            }

            // Switch enable lighting
            if (InputManager.KeyPressed(Keys.F2))
            {
                _enableLighting = !_enableLighting;
            }

            // Switch enable render target
            if (InputManager.KeyPressed(Keys.F3))
            {
                _enableRenderTarget = !_enableRenderTarget;
            }

            // Switch enable info displaying
            if (InputManager.KeyPressed(Keys.F4))
            {
                _displayInfo = !_displayInfo;
            }

            // Change ambient intensity
            if (InputManager.KeyDown(Keys.Insert))
            {
                _ambiantIntensity = MathHelper.Clamp(_ambiantIntensity + 0.01f, 0f, 1f);
            }
            else if (InputManager.KeyDown(Keys.Delete))
            {
                _ambiantIntensity = MathHelper.Clamp(_ambiantIntensity - 0.01f, 0f, 1f);
            }

            // Change directionnal light intensity
            if (InputManager.KeyDown(Keys.Home))
            {
                _diffuseIntensity = MathHelper.Clamp(_diffuseIntensity + 0.01f, 0f, 1f);
            }
            else if (InputManager.KeyDown(Keys.End))
            {
                _diffuseIntensity = MathHelper.Clamp(_diffuseIntensity - 0.01f, 0f, 1f);
            }

            // Change directionnal light direction
            if (InputManager.KeyDown(Keys.NumPad8))
            {
                _diffuseLightDirection.Z += 0.1f;
                _diffuseLightDirection.Normalize();
            }
            else if (InputManager.KeyDown(Keys.NumPad5))
            {
                _diffuseLightDirection.Z -= 0.1f;
                _diffuseLightDirection.Normalize();
            }
            else if (InputManager.KeyDown(Keys.NumPad9))
            {
                _diffuseLightDirection.Y += 0.1f;
                _diffuseLightDirection.Normalize();
            }
            else if (InputManager.KeyDown(Keys.NumPad3))
            {
                _diffuseLightDirection.Y -= 0.1f;
                _diffuseLightDirection.Normalize();
            }
            else if (InputManager.KeyDown(Keys.NumPad6))
            {
                _diffuseLightDirection.X += 0.1f;
                _diffuseLightDirection.Normalize();
            }
            else if (InputManager.KeyDown(Keys.NumPad4))
            {
                _diffuseLightDirection.X -= 0.1f;
                _diffuseLightDirection.Normalize();
            }

            // Change specular intensity
            if (InputManager.KeyDown(Keys.P))
            {
                _specularIntensity = MathHelper.Clamp(_specularIntensity + 0.01f, 0f, 1f);
            }
            else if (InputManager.KeyDown(Keys.M))
            {
                _specularIntensity = MathHelper.Clamp(_specularIntensity - 0.01f, 0f, 1f);
            }

            // Change specular shininess
            if (InputManager.KeyDown(Keys.L))
            {
                _shininess = MathHelper.Clamp(_shininess + 1, 0, 500);
            }
            else if (InputManager.KeyDown(Keys.O))
            {
                _shininess = MathHelper.Clamp(_shininess - 1, 0, 500);
            }

            // Change water height
            if (InputManager.KeyDown(Keys.PageUp))
            {
                _waterHeight += 0.1f;
                _refractionClippingPlane = CreateClippingPlane(false);
                _reflectionClippingPlane = CreateClippingPlane(true);
                SetUpVertices();
                SetUpIndices();
            }
            else if (InputManager.KeyDown(Keys.PageDown))
            {
                _waterHeight -= 0.1f;
                _refractionClippingPlane = CreateClippingPlane(false);
                _reflectionClippingPlane = CreateClippingPlane(true);
                SetUpVertices();
                SetUpIndices();
            }
            #endregion

            #region Moving

            Vector3 motion = Vector3.Zero;

            if (InputManager.KeyDown(Keys.Z))
            {
                motion.Z += _cameraSpeed * deltaTime;
            }
            if (InputManager.KeyDown(Keys.S))
            {
                motion.Z -= _cameraSpeed * deltaTime;
            }
            if (InputManager.KeyDown(Keys.Q))
            {
                motion.X += _cameraSpeed * deltaTime;
            }
            if (InputManager.KeyDown(Keys.D))
            {
                motion.X -= _cameraSpeed * deltaTime;
            }

            // Jump
            if (InputManager.KeyDown(Keys.Space))
            {
                motion.Y += _cameraSpeed * deltaTime;
            }

            // Crouch
            if (InputManager.KeyDown(Keys.LeftControl))
            {
                motion.Y -= _cameraSpeed * deltaTime;
            }
            #endregion

            #region Camera
            
            // Mouse
            float mouseX = mouse.X - _mouseState.X;
            float mouseY = mouse.Y - _mouseState.Y;

            _cameraPitch += (mouseY * 0.5f) * deltaTime;
            _cameraYaw -= (mouseX * 0.5f) * deltaTime;

            _cameraPitch = MathHelper.Clamp(_cameraPitch, MathHelper.ToRadians(-89.9f), MathHelper.ToRadians(89.9f));

            Mouse.SetPosition(_device.Viewport.Width / 2, _device.Viewport.Height / 2);

            Matrix cameraViewRotationMatrix = Matrix.CreateRotationX(_cameraPitch) * Matrix.CreateRotationY(_cameraYaw);
            Matrix cameraMoveRotationMatrix = Matrix.CreateRotationY(_cameraYaw);

            Vector3 transformedCameraDirection = Vector3.Transform(_cameraDirection, cameraViewRotationMatrix);
            _cameraPosition += Vector3.Transform(motion, cameraMoveRotationMatrix);
            _cameraTarget = transformedCameraDirection + _cameraPosition;

            _viewMatrix = Matrix.CreateLookAt(_cameraPosition, _cameraTarget, Vector3.Up);

            // Compute reflection view matrix
            Vector3 reflCameraPosition = _cameraPosition;
            reflCameraPosition.Y = -_cameraPosition.Y + _waterHeight * 2;
            Vector3 reflTargetPos = _cameraTarget;
            reflTargetPos.Y = -_cameraTarget.Y + _waterHeight * 2;

            Vector3 cameraRight = Vector3.Transform(new Vector3(1, 0, 0), cameraMoveRotationMatrix);
            Vector3 invUpVector = Vector3.Cross(cameraRight, reflTargetPos - reflCameraPosition);

            _reflectionViewMatrix = Matrix.CreateLookAt(reflCameraPosition, reflTargetPos, invUpVector);

            #endregion


            //update the wave map offsets so that they will scroll across the water
            _waveNormalMapOffset0 += _waveVelocity0 * deltaTime;
            _waveNormalMapOffset1 += _waveVelocity1 * deltaTime;

            if (_waveNormalMapOffset0.X >= 1.0f || _waveNormalMapOffset0.X <= -1.0f)
                _waveNormalMapOffset0.X = 0.0f;
            if (_waveNormalMapOffset1.X >= 1.0f || _waveNormalMapOffset1.X <= -1.0f)
                _waveNormalMapOffset1.X = 0.0f;
            if (_waveNormalMapOffset0.Y >= 1.0f || _waveNormalMapOffset0.Y <= -1.0f)
                _waveNormalMapOffset0.Y = 0.0f;
            if (_waveNormalMapOffset1.Y >= 1.0f || _waveNormalMapOffset1.Y <= -1.0f)
                _waveNormalMapOffset1.Y = 0.0f;

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            // Change rasterization setup
            _device.RasterizerState = _rasterizerState;

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.SamplerStates[0] = SamplerState.LinearWrap;

            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

            // Generate refraction texture
            DrawRefractionMap();

            // Generate reflection texture
            DrawReflectionMap();

            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

            // Draw skybox
            DrawSkybox(_viewMatrix, _projectionMatrix, _cameraPosition);

            // Draw terrain
            DrawTerrain(_basicEffect, _viewMatrix);

            // Draw water
            DrawWater();

            // Display text
            if (_displayInfo)
            {
                _spriteBatch.Begin();

                _spriteBatch.DrawString(_font, "Position: " + _cameraPosition.ToString(), new Vector2(0, 20),
                    Color.White);
                _spriteBatch.DrawString(_font, "Target: " + _cameraTarget.ToString(), new Vector2(0, 40), Color.White);
                _spriteBatch.DrawString(_font, "Yaw: " + _cameraYaw, new Vector2(0, 60), Color.White);
                _spriteBatch.DrawString(_font, "Pitch: " + _cameraPitch, new Vector2(0, 80), Color.White);
                _spriteBatch.DrawString(_font, "Water height: " + _waterHeight, new Vector2(0, 100), Color.White);
                _spriteBatch.DrawString(_font, "Ambient intensity: " + _ambiantIntensity, new Vector2(0, 120),
                    Color.White);
                _spriteBatch.DrawString(_font, "Diffuse light direction: " + _diffuseLightDirection.ToString(),
                    new Vector2(0, 140), Color.White);
                _spriteBatch.DrawString(_font, "Diffuse light intensity: " + _diffuseIntensity, new Vector2(0, 160),
                    Color.White);
                _spriteBatch.DrawString(_font, "Specular intensity: " + _specularIntensity, new Vector2(0, 180),
                    Color.White);
                _spriteBatch.DrawString(_font, "Specular shininess: " + (1 - _shininess / 500), new Vector2(0, 200),
                    Color.White);

                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        #region Draws

        private void DrawTerrain(Effect effect, Matrix viewMatrix)
        {
            effect.CurrentTechnique = effect.Techniques["ClassicTechnique"];
            effect.Parameters["Projection"].SetValue(_projectionMatrix);
            effect.Parameters["View"].SetValue(viewMatrix);
            effect.Parameters["World"].SetValue(Matrix.Identity);
            effect.Parameters["Texture"].SetValue(_terrainTexture);

            if (effect == _refractionEffect)
            {
                effect.Parameters["ClippingPlane"].SetValue(_refractionClippingPlane);
            }
            else if (effect == _reflectionEffect)
            {
                effect.Parameters["ClippingPlane"].SetValue(_reflectionClippingPlane);
            }
            else
            {
                effect.Parameters["EnableLighting"].SetValue(_enableLighting);

                // Ambient
                effect.Parameters["AmbientColor"].SetValue(_ambientColor);
                effect.Parameters["AmbientIntensity"].SetValue(_ambiantIntensity);

                // Diffuse
                effect.Parameters["DiffuseLightDirection"].SetValue(_diffuseLightDirection);
                effect.Parameters["DiffuseColor"].SetValue(_diffuseColor);
                effect.Parameters["DiffuseIntensity"].SetValue(_diffuseIntensity);

                // Specular
                effect.Parameters["Shininess"].SetValue(_shininess);
                effect.Parameters["SpecularColor"].SetValue(_specularColor);
                effect.Parameters["SpecularIntensity"].SetValue(_specularIntensity);
                effect.Parameters["ViewVector"].SetValue(_cameraTarget);

            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, _terrainVertices.Length, _terrainIndices, 0, _terrainIndices.Length / 3, VertexPositionNormalTexture.VertexDeclaration);
            }
        }

        private void DrawWater()
        {
            _waterEffect.CurrentTechnique = _waterEffect.Techniques["ClassicTechnique"];
            _waterEffect.Parameters["Projection"].SetValue(_projectionMatrix);
            _waterEffect.Parameters["View"].SetValue(_viewMatrix);
            _waterEffect.Parameters["World"].SetValue(Matrix.Identity);

            _waterEffect.Parameters["RefractionTexture"].SetValue(_refractionTexture);

            _waterEffect.Parameters["ReflectionTexture"].SetValue(_reflectionTexture);
            _waterEffect.Parameters["ReflectionMatrix"].SetValue(_reflectionViewMatrix);

            _waterEffect.Parameters["WaveNormalMap0"].SetValue(_waveNormalMap0);
            _waterEffect.Parameters["WaveNormalMap1"].SetValue(_waveNormalMap1);

            _waterEffect.Parameters["WaveMapOffset0"].SetValue(_waveNormalMapOffset0);
            _waterEffect.Parameters["WaveMapOffset1"].SetValue(_waveNormalMapOffset1);

            _waterEffect.Parameters["CameraPosition"].SetValue(_cameraPosition);

            foreach (EffectPass pass in _waterEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _waterVertices, 0, _waterVertices.Length, _waterIndices, 0, _waterIndices.Length / 3, VertexPositionNormalTexture.VertexDeclaration);
            }
        }

        private void DrawRefractionMap()
        {
            _device.SetRenderTarget(_refractionRenderTarget);

            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

            DrawSkybox(_viewMatrix, _projectionMatrix, _cameraPosition);
            DrawTerrain(_refractionEffect, _viewMatrix);

            _device.SetRenderTarget(null);
            _refractionTexture = _refractionRenderTarget;

            // Display render target to a file
            /*
            using(var fs = new FileStream(@"renderTargetRefraction #" + _renderTargetCounter + ".png", FileMode.OpenOrCreate))
            {
                _refractionTexture.SaveAsPng(fs, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
                _renderTargetCounter++;
            }
            */
        }

        private void DrawReflectionMap()
        {
            _device.SetRenderTarget(_reflectionRenderTarget);

            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

            DrawSkybox(_reflectionViewMatrix, _projectionMatrix, _cameraPosition);
            DrawTerrain(_reflectionEffect, _reflectionViewMatrix);

            _device.SetRenderTarget(null);
            _reflectionTexture = _reflectionRenderTarget;

            /*
            // Display render target to a file
            using(var fs = new FileStream(@"renderTargetReflection.png", FileMode.OpenOrCreate))
            {
                _reflectionTexture.SaveAsPng(fs, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
                _renderTargetCounter++;
            }
            */
        }

        private void DrawSkybox(Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            // Go through each pass in the effect, but we know there is only one...
            foreach (EffectPass pass in _skyboxEffect.CurrentTechnique.Passes)
            {
                // Draw all of the components of the mesh, but we know the cube really
                // only has one mesh
                foreach (ModelMesh mesh in _skyboxCube.Meshes)
                {
                    // Assign the appropriate values to each of the parameters
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        Matrix skyboxWorld = Matrix.CreateScale(_skyboxSize) * Matrix.CreateTranslation(_cameraPosition);

                        part.Effect = _skyboxEffect;
                        part.Effect.Parameters["World"].SetValue(skyboxWorld);
                        part.Effect.Parameters["View"].SetValue(view);
                        part.Effect.Parameters["Projection"].SetValue(projection);
                        part.Effect.Parameters["SkyboxTexture"].SetValue(_skyboxTexture);
                        part.Effect.Parameters["CameraPosition"].SetValue(_cameraPosition);
                    }

                    // Draw the mesh with the skybox effect
                    mesh.Draw();
                }
            }
        }

        #endregion

        private void LoadHeightData(string heightMapFile)
        {
            var stream = new FileStream(heightMapFile, FileMode.Open, FileAccess.Read);
            var reader = new BinaryReader(stream);

            // Read terrain dimension from raw file
            _terrainSize = new Point(reader.ReadUInt16(), reader.ReadUInt16());
            int size = _terrainSize.X * _terrainSize.Y;
            _terrainHeights = new float[_terrainSize.X, _terrainSize.Y];

            // Read height data from raw file
            var data = reader.ReadBytes(size);

            int i = 0;
            for (int y = 0; y < _terrainSize.Y; y++)
            {
                for (int x = 0; x < _terrainSize.X; x++, i++)
                {
                    _terrainHeights[x, y] = (_terrainMaxHeight * data[i]) / 255.0f;
                }
            }

            reader.Close();
            stream.Close();
        }

        private void SetUpVertices()
        {
            // Terrain
            _terrainVertices = new VertexPositionNormalTexture[_terrainSize.X * _terrainSize.Y];
            for (int x = 0; x < _terrainSize.X; x++)
            {
                for (int y = 0; y < _terrainSize.Y; y++)
                {
                    int i = x + y * _terrainSize.X;
                    _terrainVertices[i].Position = new Vector3(x, _terrainHeights[x, y], y);
                    _terrainVertices[i].TextureCoordinate = new Vector2((x / (float)_terrainSize.X),
                        1 - (y / (float)_terrainSize.Y));

                    // Compute normals
                    _terrainVertices[i].Normal = Vector3.Zero;

                    Vector3 normal;
                    float deltaHeight;
                    if (x > 0)
                    {
                        if (x + 1 < _terrainSize.X)
                        {
                            //deltaHeight = _terrainHeights[i - _terrainSize.Y] - _terrainHeights[i + _terrainSize.Y];
                            deltaHeight = _terrainHeights[x - 1, y] - _terrainHeights[x + 1, y];
                        }
                        else
                        {
                            //deltaHeight = _terrainHeights[i - _terrainSize.Y] - _terrainHeights[i];
                            deltaHeight = _terrainHeights[x - 1, y] - _terrainHeights[x, y];
                        }
                    }
                    else
                        deltaHeight = _terrainHeights[x, y] - _terrainHeights[x + 1, y];

                    var normalizedVector = new Vector3(0.0f, 1.0f, deltaHeight);
                    normalizedVector.Normalize();
                    _terrainVertices[i].Normal += normalizedVector;
                    if (y > 0)
                    {
                        if (y + 1 < _terrainSize.Y)
                            deltaHeight = _terrainHeights[x, y - 1] - _terrainHeights[x, y + 1];
                        else
                            deltaHeight = _terrainHeights[x, y - 1] - _terrainHeights[x, y];
                    }
                    else
                    {
                        deltaHeight = _terrainHeights[x, y] - _terrainHeights[x, y + 1];
                    }

                    normalizedVector = new Vector3(deltaHeight, 1.0f, 0.0f);
                    normalizedVector.Normalize();
                    _terrainVertices[i].Normal += normalizedVector;
                    _terrainVertices[i].Normal.Normalize();
                }
            }

            // Water
            _waterVertices = new VertexPositionNormalTexture[4];

            // Bottom left
            _waterVertices[0].Position = new Vector3(0, _waterHeight, 0);
            _waterVertices[0].TextureCoordinate = new Vector2(0, 1);
            _waterVertices[0].Normal = new Vector3(0, 0, 0);

            // Top left
            _waterVertices[1].Position = new Vector3(0, _waterHeight, _terrainSize.Y);
            _waterVertices[1].TextureCoordinate = new Vector2(0, 0);
            _waterVertices[1].Normal = new Vector3(0, 0, 0);

            // Top right
            _waterVertices[2].Position = new Vector3(_terrainSize.X, _waterHeight, _terrainSize.Y);
            _waterVertices[2].TextureCoordinate = new Vector2(1, 0);
            _waterVertices[2].Normal = new Vector3(0, 0, 0);

            // Bottom right
            _waterVertices[3].Position = new Vector3(_terrainSize.X, _waterHeight, 0);
            _waterVertices[3].TextureCoordinate = new Vector2(1, 1);
            _waterVertices[3].Normal = new Vector3(0, 0, 0);
        }

        private void SetUpIndices()
        {
            // Terrain
            _terrainIndices = new int[(_terrainSize.X - 1) * (_terrainSize.Y - 1) * 6];
            int counter = 0;
            for (int y = 0; y < _terrainSize.Y - 1; y++)
            {
                for (int x = 0; x < _terrainSize.X - 1; x++)
                {
                    int lowerLeft = x + y * _terrainSize.X;
                    int lowerRight = (x + 1) + y * _terrainSize.X;
                    int topLeft = x + (y + 1) * _terrainSize.X;
                    int topRight = (x + 1) + (y + 1) * _terrainSize.X;

                    // First triangle
                    _terrainIndices[counter++] = topLeft;
                    _terrainIndices[counter++] = lowerRight;
                    _terrainIndices[counter++] = lowerLeft;

                    // Seconde triangle
                    _terrainIndices[counter++] = topLeft;
                    _terrainIndices[counter++] = topRight;
                    _terrainIndices[counter++] = lowerRight;
                }
            }

            // Water
            _waterIndices = new int[6];

            _waterIndices[0] = 0;
            _waterIndices[1] = 1;
            _waterIndices[2] = 2;
            _waterIndices[3] = 2;
            _waterIndices[4] = 3;
            _waterIndices[5] = 0;

        }

        private void CreateRasterizerState(FillMode fillMode)
        {
            _rasterizerState = new RasterizerState()
            {
                CullMode = CullMode.CullClockwiseFace,
                FillMode = fillMode
            };
        }

        private Vector4 CreateClippingPlane(bool showUp)
        {
            var clippingPlane = new Vector4(0.0f, -1.0f, 0.0f, _waterHeight + 0.1f);

            return (showUp) ? -clippingPlane : clippingPlane;
        }

    }
}
