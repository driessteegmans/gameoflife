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

        private const int GridSize = 1000;

        private const string WindowTitle = "Conway's Game of Life - FPS {0}";

        #endregion

        #region Variables

#if DEBUG
        // Debug stopwatches to measure execution time while debugging.
        private readonly Stopwatch _frameRenderStopwatch = new Stopwatch();
        private readonly Stopwatch _simulationStopwatch = new Stopwatch();
#endif

        private Matrix4 matrix = Matrix4.CreateTranslation(0, 0, 0);
        private Vector2 MoCinitialvec = new Vector2(0f, 0f);
        private Vector2 MoCdvec = new Vector2(0f, 0f);
        private Vector3 PrevViewpoint = new Vector3(0f, 0f, 0f);

        //variables

        // Camera
        private Vector3 ViewPointV = new Vector3(-1f, -1f, 0f);
        private float ZoomMulti = 0.2f;

        // Game State
        private bool _paused = true;

        // Grid Information

        private readonly byte[,] _aliveGrid = new byte[GridSize, GridSize];
        private readonly byte[,] _neighboursGrid = new byte[GridSize, GridSize];

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
            Resize += (sender, e) => { GL.Viewport(0, 0, Width, Height); };

            // Mouse Click Events
            Mouse.ButtonDown += (sender, e) =>
            {
                MoCinitialvec = MousePosition(e.X, e.Y, this);
                MoCdvec = MoCinitialvec;


                //start moving viewpoint
                if (e.Button == MouseButton.Right)
                {
                    PrevViewpoint = ViewPointV;
                }
            };

            // Mouse wheel scrolling - zoom in / outscroll wheel - zoom in / out
            Mouse.WheelChanged += (sender, e) =>
            {
                if (ZoomMulti - e.DeltaPrecise * 0.001f > 0)
                {
                    ZoomMulti -= e.DeltaPrecise * 0.001f;
                }
            };

            // Mouse moving
            Mouse.Move += (sender, e) =>
            {
                //for adding object
                MoCdvec = MousePosition(e.X, e.Y, this);

                if (e.Mouse.LeftButton == ButtonState.Pressed)
                {
                    // TODO(BERKAN) EXPENSIVE !!! TRY - CATCH = EXPENSIVE!!
                    try
                    {
                        _aliveGrid[(int)MoCdvec.X, (int)MoCdvec.Y] = 1;
                    }
                    catch
                    {
                    }
                }


                //for moving viewpoint
                if (e.Mouse.RightButton == ButtonState.Pressed)
                {
                    ViewPointV = new Vector3((MoCdvec - MoCinitialvec).X / (Width * ZoomMulti),
                                     (MoCdvec - MoCinitialvec).Y / (Height * ZoomMulti), 0) + PrevViewpoint;
                }
            };

            // Key presses
            KeyPress += (sender, e) =>
            {
                switch (e.KeyChar)
                {
                    case 'f':
                        {
                            WindowState = WindowState != WindowState.Fullscreen
                                ? WindowState.Fullscreen
                                : WindowState.Normal;
                            break;
                        }
                    case 'p':
                        {
                            _paused = !_paused;
                            break;
                        }
                        
                    case 'c': // Clears the screen.
                        {
                            // Fill arrays with zeroes.
                            for (var i = 0; i < GridSize; i++)
                            {
                                for (var z = 0; z < GridSize; z++)
                                {
                                    _aliveGrid[i, z] = 0;
                                }
                            }

                            break;
                        }

                }
            };

            // Frame Update
            UpdateFrame += Game_UpdateFrame;

            // Rendering
            RenderFrame += Game_RenderFrame;

            // Seed grid array
            var rand = new Random();
            for (var i = 200; i < GridSize - 200; i++)
            {
                for (var z = 200; z < GridSize - 200; z++)
                {
                    _aliveGrid[i, z] = (byte)rand.Next(3);
                }
            }
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
                ViewPointV.X += 0.01f;
            }
            else if (Keyboard[Key.D])
            {
                ViewPointV.X -= 0.01f;
            }

            // Move up or down.
            if (Keyboard[Key.W])
            {
                ViewPointV.Y -= 0.01f;
            }
            else if (Keyboard[Key.S])
            {
                ViewPointV.Y += 0.01f;
            }

            // Zoom-in or out.
            if (Keyboard[Key.Z])
            {
                ZoomMulti += 0.0001f;
            }
            else if (Keyboard[Key.X] && ZoomMulti > 0.001f)
            {
                ZoomMulti -= 0.0001f;
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

            //kill / revive
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    // Swaped order to reduce calls.
                    // Any dead cell with exactly three live neighbours becomes a live cell, as if by reproduction.
                    if (_neighboursGrid[x, y] == 3)
                    {
                        _aliveGrid[x, y] = 1;
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

        private void Game_RenderFrame(object sender, FrameEventArgs e)
        {

#if DEBUG
            _frameRenderStopwatch.Restart();
#endif

            // Clear OpenGL window buffer.
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.MatrixMode(MatrixMode.Projection);

            matrix = Matrix4.CreateTranslation(ViewPointV);

            GL.LoadMatrix(ref matrix);
            GL.Ortho(-Width * ZoomMulti, Width * ZoomMulti, -Height * ZoomMulti,
                Height * ZoomMulti, 0.0, 4.0);

            // Render graphics
            // Now draw the objects to the gamewindow

            GL.Color3(Color.Yellow);

            GL.Begin(PrimitiveType.Quads);

            // Draw the cells.
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    // Check if a cell is alife.
                    if (_aliveGrid[x, y] == 1)
                    {
                        GL.Vertex2(x, y);
                        GL.Vertex2(x, y + 1);
                        GL.Vertex2(x + 1, y + 1);
                        GL.Vertex2(x + 1, y);
                    }
                }
            }

            GL.End();

            // Draw a circle at the viewpoint.
            DrawCircle(30, ViewPointV.X, ViewPointV.Y, 1);

            // OpenGL Swap the window buffers to display the new frame.
            SwapBuffers();

#if DEBUG
            _frameRenderStopwatch.Stop();
            Console.WriteLine("FrameRender: {0}ms", _frameRenderStopwatch.ElapsedMilliseconds);
#endif

            Title = string.Format(WindowTitle, Math.Round(RenderFrequency, 4));
        }

        //draw a circle on the opentk gamewindow
        public void DrawCircle(int segments, float xpos, float ypos, float radius)
        {
            GL.Begin(PrimitiveType.Polygon);

            for (int i = 0; i < segments; i++)
            {
                float theta = (2.0f * (float)Math.PI * i) / (float)segments;
                float cxx = radius * (float)Math.Cos(theta);
                float cyy = radius * (float)Math.Sin(theta);
                GL.Vertex2(xpos + cxx, ypos + cyy);
            }

            GL.End();
        }


        //get the position of the mouse as a vector scaled to the gamewindow
        public Vector2 MousePosition(float mX, float mY, GameWindow game)
        {
            Vector2 vecMousePos = new Vector2(((mX) / (float)Width - 0.5f) * Width * 2 *
                                              ZoomMulti - ViewPointV.X * Width * ZoomMulti,
                0 - ((mY) / (float)Height - 0.5f)
                * Height * 2 * ZoomMulti - ViewPointV.Y * Height * ZoomMulti);
            return vecMousePos;
        }
    }
}