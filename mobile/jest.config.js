module.exports = {
  preset: "jest-expo",
  setupFilesAfterEnv: ["<rootDir>/jest.setup.js"],
  transformIgnorePatterns: [
    "node_modules/(?!((jest-)?react-native|@react-native(-community)?|@react-navigation|@expo(nent)?|expo(nent)?|expo-router|expo-asset|expo-font|expo-modules-core|@expo-google-fonts/.*|@unimodules/.*|unimodules|nativewind|react-native-svg|react-native-gesture-handler|react-native-reanimated|react-native-css-interop|lucide-react-native)/)",
  ],
  moduleNameMapper: {
    "^@/(.*)$": "<rootDir>/$1",
  },
};
