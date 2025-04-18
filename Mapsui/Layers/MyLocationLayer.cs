﻿using Mapsui.Animations;
using Mapsui.Extensions;
using Mapsui.Styles;
using Mapsui.Utilities;
using System;
using System.Collections.Generic;
using Animation = Mapsui.Animations.Animation;

namespace Mapsui.Layers;

/// <summary>
/// A layer to display a symbol for own location
/// </summary>
/// <remarks>
/// There are two different symbols for own location: one is used when there isn't a change in position (still),
/// and one is used, if the position changes (moving).
/// </remarks>
public class MyLocationLayer : BaseLayer, IDisposable
{
    private readonly Map _map;
    private readonly PointFeature _feature;
    private readonly ImageStyle _locStyle;  // style for the location indicator
    private readonly ImageStyle _dirStyle;  // style for the view-direction indicator
    private readonly CalloutStyle _coStyle;  // style for the callout

    private static readonly string _movingImageSource = "embedded://Mapsui.Resources.Images.MyLocationMoving.svg";
    private static readonly string _stillImageSource = "embedded://Mapsui.Resources.Images.MyLocationStill.svg";
    private static readonly string _directionImageSource = "embedded://Mapsui.Resources.Images.MyLocationDir.svg";

    private MPoint? _animationStart;
    private MPoint? _animationEnd;

    private readonly ConcurrentHashSet<AnimationEntry<Map>> _animations = [];
    private readonly List<IFeature> _features;
    private AnimationEntry<Map>? _animationMyDirection;
    private AnimationEntry<Map>? _animationMyViewDirection;
    private AnimationEntry<Map>? _animationMyLocation;

    private bool _isMoving;

    /// <summary>
    /// Should be moving arrow or round circle displayed
    /// </summary>
    public bool IsMoving
    {
        get => _isMoving;
        set
        {
            if (_isMoving != value)
            {
                _isMoving = value;
                _locStyle.Image = _isMoving ? _movingImageSource : _stillImageSource;
            }
        }
    }

    private bool _isCentered = true;

    /// <summary>
    /// MyLocation is always in the center of the map
    /// </summary>
    public bool IsCentered
    {
        get => _isCentered;
        set
        {
            if (_isCentered != value)
            {
                _isCentered = value;
            }
        }
    }

    private MPoint _myLocation = new(0, 0);

    /// <summary>
    /// Position of location, that is displayed
    /// </summary>
    /// <value>Position of location</value>
    public MPoint MyLocation => _myLocation;

    /// <summary>
    /// Movement direction of device at location
    /// </summary>
    /// <value>Direction at location</value>
    public double Direction { get; private set; } = 0.0;

    /// <summary>
    /// Viewing direction of device (in degrees wrt. north direction)
    /// </summary>
    /// <value>Direction at location</value>
    public double ViewingDirection { get; private set; } = -1.0;

    /// <summary>
    /// Scale of symbol
    /// </summary>
    /// <value>Scale of symbol</value>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// The text that is displayed in the MyLocation callout
    /// (can contain line breaks).
    /// </summary>
    public string CalloutText
    {
        get => _coStyle.Title ?? "";
        set
        {
            _coStyle.Title = value;
            _map.Refresh();
        }
    }

    /// <summary>
    /// Show or hide a callout with further info next to the MyLocation symbol.
    /// </summary>
    public bool ShowCallout
    {
        get => _coStyle.Enabled;
        set
        {
            _coStyle.Enabled = value;
            _map.Refresh();
        }
    }

    /// <summary>
    /// This event is triggered whenever the MyLocation symbol or label is clicked.
    /// </summary>
    public event EventHandler<MapEventArgs>? Tapped;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Mapsui.Layers.MyLocationLayer"/> class
    /// with a starting location.
    /// </summary>
    /// <param name="map">MapView, to which this layer belongs</param>
    /// <param name="location">Location, where to start</param>
    public MyLocationLayer(Map map, MPoint location) : this(map)
    {
        _myLocation = location;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Mapsui.Layers.MyLocationLayer"/> class.
    /// </summary>
    /// <param name="map">Map, to which this layer belongs</param>
    public MyLocationLayer(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
        _map.Tapped += MapTapped;

        Enabled = true;

        _feature = new PointFeature(_myLocation)
        {
            ["Label"] = "MyLocation",
        };

        _locStyle = new ImageStyle
        {
            Enabled = true,
            Image = _stillImageSource,
            SymbolScale = Scale,
            SymbolRotation = Direction,
            Offset = new Offset(0, 0),
            Opacity = 1,
        };

        _dirStyle = new ImageStyle
        {
            Enabled = false,
            Image = _directionImageSource,
            SymbolScale = 0.2,
            SymbolRotation = 0,
            Offset = new Offset(0, 0),
            Opacity = 1,
        };

        _coStyle = new CalloutStyle
        {
            Enabled = false,
            Type = CalloutType.Single,
            Title = "",
            TitleFontColor = Color.Black,
            MaxWidth = 300,
            RotateWithMap = true,
            SymbolOffsetRotatesWithMap = true,
            Offset = new Offset(0, -SymbolStyle.DefaultHeight * 0.4f),
            BalloonDefinition = new CalloutBalloonDefinition
            {
                Color = Color.White,
                TailAlignment = TailAlignment.Top,
                TailPosition = 0,
                StrokeWidth = 0,
                ShadowWidth = 0
            },
        };

        _feature.Styles.Clear();
        _feature.Styles.Add(_dirStyle);
        _feature.Styles.Add(_locStyle);
        _feature.Styles.Add(_coStyle);

        _features = [_feature];
        Style = null;
    }

    /// <summary>
    /// Updates own location
    /// </summary>
    /// <param name="newLocation">New location</param>
    public void UpdateMyLocation(MPoint newLocation, bool animated = false)
    {
        if (!MyLocation.Equals(newLocation))
        {
            // We have a location update, so abort last animation
            if (_animationMyLocation != null)
            {
                Animation.Stop(_map, _animationMyLocation, callFinal: true);
                _animations.TryRemove(_animationMyLocation);
                _animationMyLocation = null;
            }

            if (animated)
            {
                // Save values for new animation
                _animationStart = MyLocation;
                _animationEnd = newLocation;
                var deltaX = _animationEnd.X - _animationStart.X;
                var deltaY = _animationEnd.Y - _animationStart.Y;

                if (_map.Navigator.Viewport.ToExtent() is not null)
                {
                    // Refresh the end viewport at the start of the animation so it has time to load.
                    var endViewport = _map.Navigator.Viewport with { CenterX = _animationEnd.X, CenterY = _animationEnd.Y };
                    _map.RefreshData(endViewport);

                    _animationMyLocation = new AnimationEntry<Map>(
                        MyLocation,
                        newLocation,
                        animationStart: 0,
                        animationEnd: 1,
                        tick: (map, entry, v) =>
                        {
                            var modified = InternalUpdateMyLocation(new MPoint(_animationStart.X + deltaX * v, _animationStart.Y + deltaY * v));
                            return new AnimationResult<Map>(map, true);
                        },
                        final: (map, entry) =>
                        {
                            if (!MyLocation.Equals(_animationEnd))
                            {
                                InternalUpdateMyLocation(_animationEnd);
                            }

                            return new AnimationResult<Map>(map, false);
                        });

                    Animation.Start(_animationMyLocation, 1000);
                    _animations.Add(_animationMyLocation);

                    // Update viewport
                    if (_isCentered)
                    {
                        _map?.Navigator.CenterOn(_animationEnd, 1000, Easing.Linear);
                    }
                }
            }
            else
            {
                var modified = InternalUpdateMyLocation(newLocation);

                // Update viewport
                if (_isCentered)
                {
                    _map?.Navigator.CenterOn(_myLocation);
                }
            }
        }
    }

    /// <summary>
    /// Updates my movement direction
    /// </summary>
    /// <param name="newDirection">New direction</param>
    /// <param name="newViewportRotation">New viewport rotation</param>
    /// <param name="animated">true if animated</param>
    public void UpdateMyDirection(double newDirection, double newViewportRotation, bool animated = false)
    {
        var newRotation = (int)(newDirection - newViewportRotation);
        var oldRotation = (int)_locStyle.SymbolRotation;
        var diffRotation = newDirection - oldRotation;

        if (newRotation != oldRotation)
        {
            Direction = newDirection;

            // We have a direction update, so abort last animation
            if (_animationMyDirection != null)
            {
                Animation.Stop(_map, _animationMyDirection, callFinal: true);
                _animations.TryRemove(_animationMyDirection);
                _animationMyDirection = null;
            }

            if (newRotation < 90 && oldRotation > 270)
            {
                newRotation += 360;
            }
            else if (newRotation > 270 && oldRotation < 90)
            {
                oldRotation += 360;
            }

            var endRotation = newRotation % 360;

            if (animated)
            {
                _animationMyDirection = new AnimationEntry<Map>(
                    oldRotation,
                    newRotation,
                    animationStart: 0,
                    animationEnd: 1,
                    tick: (map, entry, v) =>
                    {
                        var symbolRotation = (oldRotation + (int)(v * diffRotation)) % 360;
                        if ((int)symbolRotation != (int)_locStyle.SymbolRotation)
                        {
                            _locStyle.SymbolRotation = symbolRotation;
                            map.Refresh();
                        }

                        return new AnimationResult<Map>(map, true);
                    },
                    final: (map, v) =>
                    {
                        if ((int)_locStyle.SymbolRotation != (int)endRotation)
                        {
                            _locStyle.SymbolRotation = endRotation;
                            map.Refresh();
                        }

                        return new AnimationResult<Map>(map, false);
                    });

                Animation.Start(_animationMyDirection, 1000);
                _animations.Add(_animationMyDirection);
            }
            else
            {
                _locStyle.SymbolRotation = endRotation;
                _map.Refresh();
            }
        }
    }

    /// <summary>
    /// Updates my speed
    /// </summary>
    /// <param name="newSpeed">New speed</param>
    public void UpdateMySpeed(double newSpeed)
    {
        var modified = false;

        if (newSpeed > 0 && !IsMoving)
        {
            IsMoving = true;
            modified = true;
        }

        if (newSpeed <= 0 && IsMoving)
        {
            IsMoving = false;
            modified = true;
        }

        if (modified)
            _map.Refresh();
    }

    /// <summary>
    /// Updates my view direction
    /// </summary>
    /// <param name="newDirection">New direction</param>
    /// <param name="newViewportRotation">New viewport rotation</param>
    /// <param name="animated">true if animated</param>
    public void UpdateMyViewDirection(double newDirection, double newViewportRotation, bool animated = false)
    {
        var newRotation = (int)(newDirection - newViewportRotation);
        var oldRotation = (int)_dirStyle.SymbolRotation;
        var diffRotation = newDirection - oldRotation;

        if (newRotation == -1.0)
        {
            // disable bitmap
            _dirStyle.Enabled = false;
        }
        else if (newRotation != oldRotation)
        {
            // We have a direction update, so abort last animation
            if (_animationMyViewDirection != null)
            {
                Animation.Stop(_map, _animationMyViewDirection, callFinal: true);
                _animations.TryRemove(_animationMyViewDirection);
                _animationMyViewDirection = null;
            }

            _dirStyle.Enabled = true;
            ViewingDirection = newDirection;

            if (newRotation < 90 && oldRotation > 270)
            {
                newRotation += 360;
            }
            else if (newRotation > 270 && oldRotation < 90)
            {
                oldRotation += 360;
            }

            var endRotation = newRotation % 360;

            if (animated)
            {
                _animationMyViewDirection = new AnimationEntry<Map>(
                    oldRotation,
                    newRotation,
                    animationStart: 0,
                    animationEnd: 1,
                    tick: (map, entry, v) =>
                    {
                        var symbolRotation = (oldRotation + (int)(v * diffRotation)) % 360;
                        if ((int)symbolRotation != (int)_dirStyle.SymbolRotation)
                        {
                            _dirStyle.SymbolRotation = symbolRotation;
                            map.Refresh();
                        }

                        return new AnimationResult<Map>(map, true);
                    },
                    final: (map, v) =>
                    {
                        if ((int)_dirStyle.SymbolRotation != endRotation)
                        {
                            _dirStyle.SymbolRotation = endRotation;
                            map.Refresh();
                        }

                        return new AnimationResult<Map>(map, false);
                    });

                Animation.Start(_animationMyViewDirection, 1000);
                _animations.Add(_animationMyViewDirection);
            }
            else
            {
                _dirStyle.SymbolRotation = endRotation;
                _map.Refresh();
            }
        }
    }

    public override bool UpdateAnimations()
    {
        if (_animations.Count > 0)
        {
            var animation = Animation.UpdateAnimations(_map, _animations);
            return animation.IsRunning;
        }

        return base.UpdateAnimations();
    }

    public override IEnumerable<IFeature> GetFeatures(MRect box, double resolution)
    {
        return _features;
    }

    private bool InternalUpdateMyLocation(MPoint newLocation)
    {
        var modified = false;

        if (!_myLocation.Equals(newLocation))
        {
            _myLocation = newLocation;
            _feature.Modified();
            _feature.Point.X = _myLocation.X;
            _feature.Point.Y = _myLocation.Y;
            modified = true;
        }

        return modified;
    }

    private void MapTapped(object? s, MapEventArgs e)
    {
        var mapInfo = e.GetMapInfo([this]);
        if (mapInfo.Feature != null && mapInfo.Feature.Equals(_feature))
        {
            Tapped?.Invoke(this, e);
            e.Handled = true;
        }
    }
}
