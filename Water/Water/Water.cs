using System.Diagnostics;
using System.IO;
using System.Text;
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
        private Matrix _projection;
        private Matrix _viewMatrix;

        // Input
        private MouseState _mouseState;

        // Terrain
        private float[,] _terrainHeights;
        private Point _terrainSize;
        private float _terrainMaxHeight = 50;
        private Texture2D _terrainTexture;
        private VertexPositionNormalTexture[] _terrainVertices;
        private int[] _terrainIndices;
        private Vector4 _clippingPlane;

        // Water
        private float _waterHeight;
        private VertexPositionNormalTexture[] _waterVertices;
        private int[] _waterIndices;

        // Refraction
        RenderTarget2D _refractionRenderTarget;
        Texture2D _refractionTexture;

        // Shaders
        private Effect _basicEffect;
        private Effect _refractionEffect;

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

            // Graphics preferences
            _graphics.PreferredBackBufferWidth = 800;
            _graphics.PreferredBackBufferHeight = 600;
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges();
            Window.Title = "Water project";

            // Camera init
            _nearPlane = 1.0f;
            _farPlane = 1000.0f;
            _fieldOfView = 45.0f;

            _cameraPosition = new Vector3(0.0f, 0.0f, 0.0f);
            _cameraDirection = new Vector3(0.0f, 0.0f, 1.0f);
            _cameraTarget = _cameraDirection + _cameraPosition;

            _cameraSpeed = 100.0f;

            _cameraPitch = 0.0f;
            _cameraYaw = 0.0f;

            _aspectRatio = (float)_device.Viewport.Width / (float)_device.Viewport.Height;

            _projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(_fieldOfView), _aspectRatio, _nearPlane, _farPlane);

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
            _refractionRenderTarget = new RenderTarget2D(_device, pp.BackBufferWidth, pp.BackBufferHeight);

            CreateRasterizerState(FillMode.Solid);

            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load font file
            _font = Content.Load<SpriteFont>(@"Fonts/ClassicFont");

            // Load texture
            _terrainTexture = Content.Load<Texture2D>(@"Textures/terrain_texture");

            // Load height map
            LoadHeightData(Content.RootDirectory + "/Textures/terrain_height.raw");

            // Water
            _waterHeight = 20;
            _clippingPlane = CreatePlane(_waterHeight, Vector3.Up, _viewMatrix, true);

            // Create vertex/index buffers
            SetUpVertices();
            SetUpIndices();

            // Shaders
            _basicEffect = Content.Load<Effect>("Shaders/Lights");
            _refractionEffect = Content.Load<Effect>("Shaders/Refraction");
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
            if (!this.IsActive)
                return;

            MouseState mouse = Mouse.GetState();

            // Allows the game to exit
            if (InputManager.KeyDown(Keys.Escape))
                this.Exit();

            // Switch fill mode
            if (InputManager.KeyPressed(Keys.F1))
            {
                var newFillMode = (_rasterizerState.FillMode == FillMode.Solid)
                    ? FillMode.WireFrame
                    : FillMode.Solid;

                CreateRasterizerState(newFillMode);
            }

            #region Moving
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

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

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // Change rasterization setup
            _device.RasterizerState = _rasterizerState;

            var depthState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = true
            };

            _device.DepthStencilState = depthState;

            // Generate refraction texture
            //DrawRefractionMap();

            // Draw terrain
            DrawTerrain(_refractionEffect, _viewMatrix);

            // Draw water
            //DrawWater();

            // Display text
            _spriteBatch.Begin();

            _spriteBatch.DrawString(_font, "Position: " + _cameraPosition.ToString(), new Vector2(0, 20), Color.White);
            _spriteBatch.DrawString(_font, "Target: " + _cameraTarget.ToString(), new Vector2(0, 40), Color.White);
            _spriteBatch.DrawString(_font, "Yaw: " + _cameraYaw, new Vector2(0, 60), Color.White);
            _spriteBatch.DrawString(_font, "Pitch: " + _cameraPitch, new Vector2(0, 80), Color.White);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

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
                    _terrainVertices[i].TextureCoordinate = new Vector2(((float)x / (float)_terrainSize.X), 1 - ((float)y / (float)_terrainSize.Y));

                    // Compute normals
                    _terrainVertices[i].Normal = Vector3.Zero;
                }
            }

            // Water
            _waterVertices = new VertexPositionNormalTexture[4];

            // Bottom left
            _waterVertices[0].Position = new Vector3(0, _waterHeight, 0);
            _waterVertices[0].TextureCoordinate = new Vector2(0, 0);
            _waterVertices[0].Normal = new Vector3(0, 0, 0);

            // Top left
            _waterVertices[1].Position = new Vector3(0, _waterHeight, _terrainSize.Y);
            _waterVertices[1].TextureCoordinate = new Vector2(0, 1);
            _waterVertices[1].Normal = new Vector3(0, 0, 0);

            // Top right
            _waterVertices[2].Position = new Vector3(_terrainSize.X, _waterHeight, _terrainSize.Y);
            _waterVertices[2].TextureCoordinate = new Vector2(1, 1);
            _waterVertices[2].Normal = new Vector3(0, 0, 0);

            // Bottom right
            _waterVertices[3].Position = new Vector3(_terrainSize.X, _waterHeight, 0);
            _waterVertices[3].TextureCoordinate = new Vector2(1, 0);
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

        private Vector4 CreatePlane(float height, Vector3 planeNormalDirection, Matrix currentViewMatrix, bool clipSide)
        {
            planeNormalDirection.Normalize();
            var planeCoeffs = new Vector4(planeNormalDirection, height);
            if (clipSide)
                planeCoeffs *= -1;

            Matrix worldViewProjection = currentViewMatrix * _projection;
            Matrix inverseWorldViewProjection = Matrix.Invert(worldViewProjection);
            inverseWorldViewProjection = Matrix.Transpose(inverseWorldViewProjection);

            var plane = Vector4.Transform(planeCoeffs, inverseWorldViewProjection);

            return new Vector4(0.0f, -1.0f, 0.0f, _waterHeight + 0.1f);
        }

        #region Draws

        private void DrawTerrain(Effect effect, Matrix viewMatrix)
        {
            effect.CurrentTechnique = effect.Techniques["ClassicTechnique"];
            effect.Parameters["Projection"].SetValue(_projection);
            effect.Parameters["View"].SetValue(viewMatrix);
            effect.Parameters["World"].SetValue(Matrix.Identity);
            effect.Parameters["Texture"].SetValue(_terrainTexture);

            if (effect == _refractionEffect)
            {
                effect.Parameters["ClippingPlane"].SetValue(_clippingPlane);
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _terrainVertices, 0, _terrainVertices.Length, _terrainIndices, 0, _terrainIndices.Length / 3, VertexPositionNormalTexture.VertexDeclaration);
            }
        }

        private void DrawWater()
        {
            _basicEffect.CurrentTechnique = _basicEffect.Techniques["ClassicTechnique"];
            _basicEffect.Parameters["Projection"].SetValue(_projection);
            _basicEffect.Parameters["View"].SetValue(_viewMatrix);
            _basicEffect.Parameters["World"].SetValue(Matrix.Identity);
            _basicEffect.Parameters["Texture"].SetValue(_refractionTexture);

            foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _waterVertices, 0, _waterVertices.Length, _waterIndices, 0, _waterIndices.Length / 3, VertexPositionNormalTexture.VertexDeclaration);
            }
        }

        private void DrawRefractionMap()
        {
            _device.SetRenderTarget(_refractionRenderTarget);
            _device.Clear(Color.CornflowerBlue);
            DrawTerrain(_refractionEffect, _viewMatrix);

            _device.SetRenderTarget(null);
            _refractionTexture = _refractionRenderTarget;

            // Display render target to a file
            /*
            using(var fs = new FileStream(@"renderTarget.png", FileMode.OpenOrCreate))
            {
                _refractionTexture.SaveAsPng(fs, 1024, 1024);
            }
            */
        }

        #endregion
    }
}
