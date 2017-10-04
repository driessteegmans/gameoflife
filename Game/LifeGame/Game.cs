/*
 * AIMIS / LIFE
 * Copyright (C) 2014, 2015 Alexis Enston
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace LifeGame
{
    public class Game : GameWindow
    {
        #region Constants

        private const float ZoomMinimum = 0.25f;
        private const float ZoomMaximum = 10f;

        private const int GridSize = 1000;
        private const int GridOffset = 200;

        private const float MoveSpeed = 0.001f;
        private const float ScrollSpeed = 0.01f;

        private const string WindowTitle = "Conway's Game of Life - FPS {0}";

        #endregion

        #region Variables

#if DEBUG
        // Debug stopwatches to measure execution time while debugging.
        private readonly Stopwatch _frameRenderStopwatch = new Stopwatch();
        private readonly Stopwatch _simulationStopwatch = new Stopwatch();
#endif
        private readonly Random _random = new Random();

        // Camera
        private Matrix4 _transformationMatrix = Matrix4.Identity;
        private Matrix4 _projectionMatrix = Matrix4.Identity;
        private Vector3 _moveTranslation = new Vector3(Vector3.Zero);
        private float _halfWidth = 1;
        private float _halfHeight = 1;
        private float _renderScale = 1.0f;

        // Game State
        private bool _paused = true;

        // Grid Information
        private byte[,] _aliveGrid = new byte[GridSize, GridSize];
        private readonly byte[,] _neighboursGrid = new byte[GridSize, GridSize];
        private readonly List<Vector2> _aliveList = new List<Vector2>();
        private bool _transformUpdateRequired;


        #endregion

        #region Constructor

        public Game()
            : base(700, 500, GraphicsMode.Default, string.Format(WindowTitle, 0))
        {
            // Setup window information
            TargetUpdateFrequency = 20;
            TargetRenderFrequency = 60;
            VSync = VSyncMode.On;

            // Window Resize
            Resize += (sender, e) =>
            {
                GL.Viewport(0, 0, Width, Height);

                _halfWidth = (float)Width / 2;
                _halfHeight = (float)Height / 2;

                _projectionMatrix = Matrix4.CreateOrthographicOffCenter(0, Width, Height, 0, -ZoomMaximum, 1);
                CalculateTransform();
            };

            // Mouse wheel scrolling - zoom in / outscroll wheel - zoom in / out
            Mouse.WheelChanged += (sender, e) =>
            {
                // Calculate the new zoom scale.
                _renderScale += e.DeltaPrecise * ScrollSpeed;

                // Cap zoom scale to 1.
                if (_renderScale < ZoomMinimum)
                {
                    _renderScale = ZoomMinimum;
                }
                else if (_renderScale > ZoomMaximum)
                {
                    _renderScale = ZoomMaximum;
                }

                GL.PointSize(_renderScale);

                // Calculate new transformation Matrix.
                CalculateTransform();
            };

            // Mouse moving
            Mouse.Move += (sender, e) =>
            {
                // Add call to the grid.
                if (e.Mouse.LeftButton == ButtonState.Pressed)
                {
                    var gridCoords = MouseToGridCoords(e.X, e.Y);

                    var gridX = (int)gridCoords.X;
                    var gridY = (int)gridCoords.Y;

                    if (gridX > 0 && gridY > 0 && gridX < GridSize && gridY < GridSize)
                    {
                        if (_aliveGrid[gridX, gridY] != 1)
                        {
                            CreateCell(gridX, gridY);
                        }
                    }
                }

                //for moving viewpoint
                if (e.Mouse.RightButton == ButtonState.Pressed)
                {
                    _moveTranslation = Vector3.Add(_moveTranslation, new Vector3(MoveSpeed * e.XDelta, MoveSpeed * -e.YDelta, 0));
                    CalculateTransform();
                }
            };

            // Key presses
            KeyPress += (sender, e) =>
            {
                switch (e.KeyChar)
                {
                    case 'F':
                    case 'f':
                        {
                            WindowState = WindowState != WindowState.Fullscreen
                                ? WindowState.Fullscreen
                                : WindowState.Normal;
                            break;
                        }

                    case 'P':
                    case 'p':
                        {
                            _paused = !_paused;
                            break;
                        }

                    case 'C':
                    case 'c': // Clears the screen.
                        {
                            // Fill arrays with zeroes.
                            ClearGrid();
                            break;
                        }

                    case 'V':
                    case 'v':
                        {
                            SeedGrid();
                            break;
                        }
                }
            };

            // Frame Update
            UpdateFrame += Game_UpdateFrame;

            // Rendering
            RenderFrame += Game_RenderFrame;
        }

        #endregion

        private void Game_UpdateFrame(object sender, FrameEventArgs e)
        {

            if (Keyboard[Key.Escape])
            {
                Exit();
            }

            // Move left or right.
            if (Keyboard[Key.A])
            {
                _moveTranslation.X += 0.01f;
                CalculateTransform();
            }
            else if (Keyboard[Key.D])
            {
                _moveTranslation.X -= 0.01f;
                CalculateTransform();
            }

            // Move up or down.
            if (Keyboard[Key.W])
            {
                _moveTranslation.Y -= 0.01f;
                CalculateTransform();
            }
            else if (Keyboard[Key.S])
            {
                _moveTranslation.Y += 0.01f;
                CalculateTransform();
            }

            if (_transformUpdateRequired)
            {
                _transformUpdateRequired = false;
                _transformationMatrix = Matrix4.CreateScale(_renderScale) * _projectionMatrix * Matrix4.CreateTranslation(_moveTranslation);
            }

            // Do not run the simulations if the game is paused.
            if (_paused)
                return;

            // Run the simulation.
#if DEBUG
            _simulationStopwatch.Restart();
#endif

            //calculate neighbours
            for (int x = 0; x < GridSize; x++)
            {
                if (x < 1)//check if x - 1 > 0 and stays in bounds and skip x - 1
                {
                    for (int y = 0; y < GridSize; y++)
                    {
                        if (_aliveGrid[x, y] == 1 //check if cell was alive in previous update cycle
                            && y > 1 //check if y - 1 > 0 and stays in bounds
                            && y < GridSize - 1) //check if y + 1 < GridSize
                        {
                            _neighboursGrid[x, y + 1]++;
                            _neighboursGrid[x, y - 1]++;
                            _neighboursGrid[x + 1, y - 1]++;
                            _neighboursGrid[x + 1, y]++;
                            _neighboursGrid[x + 1, y + 1]++;
                        }
                    }
                }
                else if (x >= GridSize - 1) //check if x + 1 < GridSize and skip x + 1
                {
                    for (int y = 0; y < GridSize; y++)
                    {
                        if (_aliveGrid[x, y] == 1 //check if cell was alive in previous update cycle
                            && y > 1 //check if y - 1 > 0 and stays in bounds
                            && y < GridSize - 1) //check if y + 1 < GridSize
                        {
                            _neighboursGrid[x - 1, y - 1]++;
                            _neighboursGrid[x - 1, y]++;
                            _neighboursGrid[x - 1, y + 1]++;
                            _neighboursGrid[x, y + 1]++;
                            _neighboursGrid[x, y - 1]++;
                        }
                    }
                }
                else //if there is no risk for an out of bounds on x run this
                {
                    for (int y = 0; y < GridSize; y++)
                    {
                        if (_aliveGrid[x, y] == 1 //check if cell was alive in previous update cycle
                            && y > 1 //check if y - 1 > 0 and stays in bounds
                            && y < GridSize - 1) //check if y + 1 < GridSize
                        {
                            _neighboursGrid[x - 1, y - 1]++;
                            _neighboursGrid[x - 1, y]++;
                            _neighboursGrid[x - 1, y + 1]++;
                            _neighboursGrid[x, y + 1]++;
                            _neighboursGrid[x, y - 1]++;
                            _neighboursGrid[x + 1, y - 1]++;
                            _neighboursGrid[x + 1, y]++;
                            _neighboursGrid[x + 1, y + 1]++;
                        }
                    }
                }
            }

            // Clear alive list.
            _aliveList.Clear();

            //kill / revive
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    // Swaped order to reduce calls.
                    // Any dead cell with exactly three live neighbours becomes a live cell, as if by reproduction.
                    if (_neighboursGrid[x, y] == 3)
                    {
                        CreateCell(x, y);
                    }
                    else if (_neighboursGrid[x, y] < 2 || _neighboursGrid[x, y] > 3)
                    {
                        _aliveGrid[x, y] = 0;
                    }

                    _neighboursGrid[x, y] = 0; // Reset neighbours.
                }
            }

#if DEBUG
            _simulationStopwatch.Stop();
            Console.WriteLine("Simulation: {0}ms", _simulationStopwatch.ElapsedMilliseconds);
#endif
        }

        private void CreateCell(int x, int y)
        {
            _aliveGrid[x, y] = 1;

            _aliveList.Add(new Vector2
            {
                X = x,
                Y = y
            });
        }

        private void Game_RenderFrame(object sender, FrameEventArgs e)
        {

#if DEBUG
            _frameRenderStopwatch.Restart();
#endif

            // Clear OpenGL window buffer.
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref _transformationMatrix);

            // Render graphics

            GL.Color3(Color.Yellow);
            GL.Begin(PrimitiveType.Points);

            // Draw the cells.
            foreach (var cell in _aliveList)
            {
                GL.Vertex2(cell.X, cell.Y);
            }

            GL.End();

            // OpenGL Swap the window buffers to display the new frame.
            SwapBuffers();

#if DEBUG
            _frameRenderStopwatch.Stop();
            Console.WriteLine("FrameRender: {0}ms", _frameRenderStopwatch.ElapsedMilliseconds);
#endif

            Title = string.Format(WindowTitle, Math.Round(RenderFrequency, 4));
        }

        /// <summary>
        /// Get the mouse position as a vector scaled to the zoom.
        /// </summary>
        /// <param name="mouseX"></param>
        /// <param name="mouseY"></param>
        /// <returns></returns>
        public Vector2 MouseToGridCoords(float mouseX, float mouseY)
        {
            var translationOffsetX = (float)Math.Round(_halfWidth * _moveTranslation.X);
            var translationOffsetY = (float)Math.Round(_halfHeight * _moveTranslation.Y);

            return new Vector2(mouseX / _renderScale - translationOffsetX, mouseY / _renderScale + translationOffsetY);
        }

        private void ClearGrid()
        {
            _aliveGrid = new byte[GridSize, GridSize];
            _aliveList.Clear();
        }

        private void SeedGrid()
        {
            // Clear the old grid.
            ClearGrid();

            const int target = GridSize - GridOffset;

            // Seed the grid with random data.
            for (var x = GridOffset; x < target; x++)
            {
                for (var y = GridOffset; y < target; y++)
                {
                    var value = (byte)_random.Next(3);

                    if (value == 1)
                    {
                        CreateCell(x, y);
                    }
                    else
                    {
                        _aliveGrid[x, y] = value;
                    }
                }
            }
        }

        private void CalculateTransform()
        {
            _transformUpdateRequired = true;
        }
    }
}