import renderer, { act } from "react-test-renderer";
import { describe, it, expect, jest } from "@jest/globals";
import { ScrollView, Text, View } from "react-native";

import {
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableRow,
  TableHead,
  TableCell,
  TableCaption,
} from "../table";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Table", () => {
  it("renders a horizontally scrollable container and forwards className", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Table className="custom-class">
          <TableHeader>
            <TableRow>
              <TableHead>Head</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell>Cell</TableCell>
            </TableRow>
          </TableBody>
          <TableFooter>
            <TableCell>Footer</TableCell>
          </TableFooter>
          <TableCaption>Caption</TableCaption>
        </Table>,
      );
    });

    const scrollView = tree.root.findByType(ScrollView);
    expect(scrollView.props.horizontal).toBe(true);
    expect(scrollView.props.showsHorizontalScrollIndicator).toBe(true);
    expect(scrollView.props.contentContainerClassName).toContain("min-w-full");
    expect(scrollView.props.className).toContain("w-full");
    expect(scrollView.props.className).toContain("custom-class");
  });

  it("wraps string children of head and cell in styled Text components", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <View>
          <TableHead>Column</TableHead>
          <TableCell>Row value</TableCell>
        </View>,
      );
    });

    const texts = tree.root.findAllByType(Text);
    const headText = texts.find((t) => t.props.children === "Column");
    const cellText = texts.find((t) => t.props.children === "Row value");

    expect(headText?.props.className).toContain(
      "text-sm font-medium text-gray-500",
    );
    expect(cellText?.props.className).toContain("text-sm text-gray-900");
  });

  it("applies structural styles to sections and rows", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Table>
          <TableHeader className="header-extra" />
          <TableBody className="body-extra" />
          <TableFooter className="footer-extra" />
          <TableRow className="row-extra" />
          <TableCaption className="caption-extra">Note</TableCaption>
        </Table>,
      );
    });

    const views = tree.root.findAllByType(View);
    const header = views.find((v) => v.props.className?.includes("header-extra"));
    const body = views.find((v) => v.props.className?.includes("body-extra"));
    const footer = views.find((v) => v.props.className?.includes("footer-extra"));
    const row = views.find((v) => v.props.className?.includes("row-extra"));
    //const caption = tree.root.findByType(Text);
    const texts = tree.root.findAllByType(Text);
    const caption = texts.find((t) => t.props.children === "Note");

    if (!caption) {
      throw new Error("Expected TableCaption text to be rendered.");
    }

    expect(header?.props.className).toContain("border-b");
    expect(body?.props.className).toContain("flex-col");
    expect(footer?.props.className).toContain("border-t");
    expect(row?.props.className).toContain("flex-row");
    expect(caption.props.className).toContain("text-center");
    expect(caption.props.className).toContain("caption-extra");
  });
});
