import {
  useState,
  useRef,
  useCallback,
  createContext,
  useContext,
  useEffect,
  Children,
  isValidElement,
  cloneElement,
} from "react";
import {
  View,
  FlatList,
  Pressable,
  ViewProps,
  NativeSyntheticEvent,
  NativeScrollEvent,
  PressableProps,
} from "react-native";
import { ArrowLeft, ArrowRight } from "lucide-react-native";
import { cn } from "@/utils/cn";

type CarouselContextProps = {
  carouselRef: React.RefObject<FlatList<any> | null>;
  currentIndex: number;
  scrollPrev: () => void;
  scrollNext: () => void;
  canScrollPrev: boolean;
  canScrollNext: boolean;
  itemWidth: number;
  setItemWidth: (width: number) => void;
};

const CarouselContext = createContext<CarouselContextProps | null>(null);

function useCarousel() {
  const context = useContext(CarouselContext);
  if (!context) {
    throw new Error("useCarousel must be used within a <Carousel />");
  }
  return context;
}

function Carousel({
  children,
  className,
  ...props
}: ViewProps & { className?: string }) {
  const carouselRef = useRef<FlatList>(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [itemWidth, setItemWidth] = useState(0);
  const [itemCount, setItemCount] = useState(0);

  const canScrollPrev = currentIndex > 0;
  const canScrollNext = currentIndex < itemCount - 1;

  const scrollPrev = useCallback(() => {
    if (canScrollPrev) {
      const nextIndex = Math.max(0, currentIndex - 1);
      carouselRef.current?.scrollToIndex({ index: nextIndex, animated: true });
    }
  }, [canScrollPrev, currentIndex]);

  const scrollNext = useCallback(() => {
    if (canScrollNext) {
      const nextIndex = Math.min(itemCount - 1, currentIndex + 1);
      carouselRef.current?.scrollToIndex({ index: nextIndex, animated: true });
    }
  }, [canScrollNext, currentIndex, itemCount]);

  return (
    <CarouselContext.Provider
      value={{
        carouselRef,
        currentIndex,
        scrollPrev,
        scrollNext,
        canScrollPrev,
        canScrollNext,
        itemWidth,
        setItemWidth,
      }}
    >
      <View className={cn("relative w-full", className)} {...props}>
        {Children.map(children, (child) => {
          if (isValidElement(child) && child.type === CarouselContent) {
            return cloneElement(child as React.ReactElement<any>, {
              setItemCount,
              setCurrentIndex,
            });
          }
          return child;
        })}
      </View>
    </CarouselContext.Provider>
  );
}

function CarouselContent({
  children,
  className,
  setItemCount,
  setCurrentIndex,
  ...props
}: ViewProps & {
  className?: string;
  setItemCount?: (count: number) => void;
  setCurrentIndex?: (index: number) => void;
}) {
  const { carouselRef, setItemWidth, itemWidth } = useCarousel();

  const items = Children.toArray(children);

  useEffect(() => {
    setItemCount?.(items.length);
  }, [items.length, setItemCount]);

  const handleScroll = (event: NativeSyntheticEvent<NativeScrollEvent>) => {
    if (!itemWidth) return;
    const offsetX = event.nativeEvent.contentOffset.x;
    const index = Math.round(offsetX / itemWidth);
    if (index >= 0 && index < items.length) {
      setCurrentIndex?.(index);
    }
  };

  return (
    <View
      className={cn("overflow-hidden flex-1 w-full h-full", className)}
      onLayout={(e) => {
        const width = e.nativeEvent.layout.width;
        if (width > 0 && width !== itemWidth) {
          setItemWidth(width);
        }
      }}
      {...props}
    >
      {itemWidth > 0 && (
        <FlatList
          ref={carouselRef}
          data={items}
          horizontal
          pagingEnabled
          showsHorizontalScrollIndicator={false}
          snapToAlignment="center"
          decelerationRate="fast"
          keyExtractor={(_, i) => i.toString()}
          onMomentumScrollEnd={handleScroll}
          className="w-full h-full"
          contentContainerClassName="items-center"
          renderItem={({ item }) => (
            // width must remain in style as it is a runtime dynamic variable
            <View className="h-full" style={{ width: itemWidth }}>
              {item}
            </View>
          )}
        />
      )}
    </View>
  );
}

function CarouselItem({
  children,
  className,
  ...props
}: ViewProps & { className?: string }) {
  return (
    <View className={cn("flex-1 justify-center px-1", className)} {...props}>
      {children}
    </View>
  );
}

function CarouselPrevious({
  className,
  ...props
}: PressableProps & { className?: string }) {
  const { scrollPrev, canScrollPrev } = useCarousel();
  return (
    <Pressable
      onPress={scrollPrev}
      disabled={!canScrollPrev}
      className={cn(
        "absolute w-8 h-8 rounded-full bg-white items-center justify-center border border-zinc-200 z-10 top-1/2 -mt-4 shadow-sm left-2.5 active:bg-[#F4F4F5]",
        !canScrollPrev && "opacity-50",
        className
      )}
      {...props}
    >
      <ArrowLeft size={16} color={!canScrollPrev ? "#ccc" : "#000"} />
    </Pressable>
  );
}

function CarouselNext({
  className,
  ...props
}: PressableProps & { className?: string }) {
  const { scrollNext, canScrollNext } = useCarousel();
  return (
    <Pressable
      onPress={scrollNext}
      disabled={!canScrollNext}
      className={cn(
        "absolute w-8 h-8 rounded-full bg-white items-center justify-center border border-zinc-200 z-10 top-1/2 -mt-4 shadow-sm right-2.5 active:bg-[#F4F4F5]",
        !canScrollNext && "opacity-50",
        className
      )}
      {...props}
    >
      <ArrowRight size={16} color={!canScrollNext ? "#ccc" : "#000"} />
    </Pressable>
  );
}

export {
  Carousel,
  CarouselContent,
  CarouselItem,
  CarouselPrevious,
  CarouselNext,
};
