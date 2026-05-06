import renderer, { act } from "react-test-renderer";
import {
  View,
  type GestureResponderEvent,
  type PanResponderGestureState,
  PanResponder, 
  type NativeTouchEvent,
} from "react-native";
import { Slider } from "../slider";
import { jest, describe, it, expect } from "@jest/globals";

type PanHandlerFn = (
  event: GestureResponderEvent,
  state: PanResponderGestureState
) => void;

const initialGestureState: PanResponderGestureState = {
    dx: 0, dy: 0,
    vx: 0, vy: 0,
    gestureState: 0,
    numberActiveTouches: 1,
    stateID: 0,
    moveX: 0, moveY: 0,
    x0: 0, y0: 0,
    _accountsForMovesUpTo: 0,
} as PanResponderGestureState;

const mockPanResponderHandlers = {
  onPanResponderGrant: jest.fn() as jest.Mock<PanHandlerFn>,
  onPanResponderMove: jest.fn() as jest.Mock<PanHandlerFn>,
  onStartShouldSetPanResponder: jest.fn(() => true),
  onMoveShouldSetPanResponder: jest.fn(() => true),
  onStartShouldSetPanResponderCapture: jest.fn(() => true),
  onMoveShouldSetPanResponderCapture: jest.fn(() => true),
  onPanResponderRelease: jest.fn(),
};

jest.spyOn(PanResponder, "create").mockReturnValue({
  panHandlers: mockPanResponderHandlers,
} as unknown as ReturnType<typeof PanResponder.create>);

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const mockNativeTouchEvent: NativeTouchEvent = {
  identifier: "1",
  timestamp: Date.now(),
  touches: [],
  changedTouches: [],
  locationX: 0,
  locationY: 0,
  pageX: 0,
  pageY: 0,
  target: 0 as any,
};

const mockGestureEvent: GestureResponderEvent = {
  currentTarget: 0,
  target: 0,
  bubbles: true,
  cancelable: true,
  defaultPrevented: false,
  eventPhase: 2,
  isDefaultPrevented: jest.fn(() => false),
  isPropagationStopped: jest.fn(() => false),
  isTrusted: true,
  timeStamp: Date.now(),
  type: "press",
  nativeEvent: mockNativeTouchEvent,
  preventDefault: jest.fn(),
  stopPropagation: jest.fn(),
} as unknown as GestureResponderEvent;

describe("Slider", () => {

  it("updates value via pan gesture and calls onValueChange respecting step", () => {
    const onValueChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    mockPanResponderHandlers.onPanResponderMove.mockImplementation(
      (_: GestureResponderEvent, gestureState: PanResponderGestureState) => {
        const trackWidth = 200;
        const min = 0;
        const max = 100;
        const step = 10;
        const startValue = 20; 

        const diffRatio = gestureState.dx / trackWidth; 
        const diffValue = diffRatio * (max - min);
        let newValue = startValue + diffValue;

        newValue = Math.max(min, Math.min(max, newValue));
        if (step > 0) {
          newValue = Math.round(newValue / step) * step; 
        }

        const latestValue = 20;

        if (newValue !== latestValue) {
          onValueChange([newValue]);
        }
      }
    );

    act(() => {
      tree = renderer.create(
        <Slider
          value={20}
          min={0}
          max={100}
          step={10}
          onValueChange={onValueChange}
        />
      );
    });

    const track = tree.root
      .findAllByType(View)
      .find((view) => typeof view.props.onLayout === "function");

    act(() => {
      track!.props.onLayout({
        nativeEvent: { layout: { width: 200, height: 10, x: 0, y: 0 } },
      });
    });

    act(() => {
      mockPanResponderHandlers.onPanResponderGrant(mockGestureEvent, initialGestureState);
      mockPanResponderHandlers.onPanResponderMove(mockGestureEvent, {
        dx: 100,
        dy: 0,
      } as PanResponderGestureState);
    });
    expect(onValueChange).toHaveBeenCalledWith([70]);
    expect(mockPanResponderHandlers.onPanResponderGrant).toHaveBeenCalled();
    expect(mockPanResponderHandlers.onPanResponderMove).toHaveBeenCalled();
  });
});