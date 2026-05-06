import renderer, { act } from "react-test-renderer";
import { Text, View } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";

import {
  Carousel,
  CarouselContent,
  CarouselItem,
  CarouselPrevious,
  CarouselNext,
} from "../carousel";

// Silence animation warnings
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock icons
jest.mock("lucide-react-native", () => {
  const { Text } = require("react-native");
  const make = (name: string) => (props: any) => (
    <Text testID={`icon-${name}`} {...props}>
      {name}
    </Text>
  );
  return { ArrowLeft: make("ArrowLeft"), ArrowRight: make("ArrowRight") };
});

afterEach(() => {
  jest.clearAllMocks();
});

describe("Carousel", () => {
  it("renders carousel structure with navigation buttons", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Carousel>
          <CarouselContent testID="carousel-content">
            <CarouselItem>
              <Text>Item 1</Text>
            </CarouselItem>
            <CarouselItem>
              <Text>Item 2</Text>
            </CarouselItem>
          </CarouselContent>
          <CarouselPrevious testID="prev-btn" />
          <CarouselNext testID="next-btn" />
        </Carousel>
      );
    });

    // Verify carousel structure is rendered
    const content = tree.root.findByProps({ testID: "carousel-content" });
    expect(content).toBeTruthy();

    // Verify navigation buttons exist
    const prevBtn = tree.root.findByProps({ testID: "prev-btn" });
    const nextBtn = tree.root.findByProps({ testID: "next-btn" });
    expect(prevBtn).toBeTruthy();
    expect(nextBtn).toBeTruthy();

    // Verify arrow icons are rendered
    const arrowLeftIcon = tree.root.findByProps({ testID: "icon-ArrowLeft" });
    const arrowRightIcon = tree.root.findByProps({ testID: "icon-ArrowRight" });
    expect(arrowLeftIcon).toBeTruthy();
    expect(arrowRightIcon).toBeTruthy();
  });

  it("renders CarouselPrevious with ArrowLeft icon", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Carousel>
          <CarouselContent>
            <CarouselItem>
              <Text>Item</Text>
            </CarouselItem>
          </CarouselContent>
          <CarouselPrevious testID="prev-btn" />
        </Carousel>
      );
    });

    const prevBtn = tree.root.findByProps({ testID: "prev-btn" });
    expect(prevBtn).toBeTruthy();

    // Find the ArrowLeft icon within the prev button
    const arrowIcon = tree.root.findByProps({ testID: "icon-ArrowLeft" });
    expect(arrowIcon).toBeTruthy();
  });

  it("renders CarouselNext with ArrowRight icon", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Carousel>
          <CarouselContent>
            <CarouselItem>
              <Text>Item</Text>
            </CarouselItem>
          </CarouselContent>
          <CarouselNext testID="next-btn" />
        </Carousel>
      );
    });

    const nextBtn = tree.root.findByProps({ testID: "next-btn" });
    expect(nextBtn).toBeTruthy();

    // Find the ArrowRight icon within the next button
    const arrowIcon = tree.root.findByProps({ testID: "icon-ArrowRight" });
    expect(arrowIcon).toBeTruthy();
  });

  it("applies custom className to Carousel", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Carousel className="custom-class" testID="carousel">
          <CarouselContent>
            <CarouselItem>
              <Text>Item</Text>
            </CarouselItem>
          </CarouselContent>
        </Carousel>
      );
    });

    // Find the outer View that has the custom class
    const views = tree.root.findAllByType(View);
    const carouselView = views.find(
      (v) =>
        v.props.className &&
        v.props.className.includes("relative") &&
        v.props.className.includes("custom-class")
    );
    expect(carouselView).toBeTruthy();
  });

  it("renders both navigation buttons together", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Carousel>
          <CarouselContent>
            <CarouselItem>
              <Text>Item 1</Text>
            </CarouselItem>
            <CarouselItem>
              <Text>Item 2</Text>
            </CarouselItem>
          </CarouselContent>
          <CarouselPrevious testID="prev-btn" />
          <CarouselNext testID="next-btn" />
        </Carousel>
      );
    });

    const prevBtn = tree.root.findByProps({ testID: "prev-btn" });
    const nextBtn = tree.root.findByProps({ testID: "next-btn" });

    expect(prevBtn).toBeTruthy();
    expect(nextBtn).toBeTruthy();

    // Both icons should be present
    const arrowLeft = tree.root.findByProps({ testID: "icon-ArrowLeft" });
    const arrowRight = tree.root.findByProps({ testID: "icon-ArrowRight" });
    expect(arrowLeft).toBeTruthy();
    expect(arrowRight).toBeTruthy();
  });

  it("can render carousel without navigation buttons", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Carousel>
          <CarouselContent testID="carousel-content">
            <CarouselItem>
              <Text>Item 1</Text>
            </CarouselItem>
          </CarouselContent>
        </Carousel>
      );
    });

    // Content should exist
    const content = tree.root.findByProps({ testID: "carousel-content" });
    expect(content).toBeTruthy();

    // Navigation buttons should not exist
    const prevBtns = tree.root.findAllByProps({ testID: "prev-btn" });
    const nextBtns = tree.root.findAllByProps({ testID: "next-btn" });
    expect(prevBtns.length).toBe(0);
    expect(nextBtns.length).toBe(0);
  });
});
