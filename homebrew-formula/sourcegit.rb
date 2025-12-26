# typed: false
# frozen_string_literal: true

# Cross-platform Homebrew formula for SourceGit
#
# Note: This formula uses pre-built binaries because SourceGit requires .NET 10,
# which is not yet available in Homebrew. Once Homebrew adds .NET 10 support,
# this formula could be refactored to build from source and potentially
# submitted to homebrew-core.
#
# For now, this formula is intended to be used via a third-party tap:
#   brew tap sourcegit-scm/sourcegit
#   brew install sourcegit

class Sourcegit < Formula
  desc "Open-source Git GUI client"
  homepage "https://github.com/sourcegit-scm/sourcegit"
  version "2025.40"
  license "MIT"

  on_macos do
    on_arm do
      url "https://github.com/sourcegit-scm/sourcegit/releases/download/v#{version}/sourcegit_#{version}.osx-arm64.zip"
      sha256 "cb04199770c0c55f660e084b12aec07bc175f901ba2e73ffec628c74c336f08f"
    end
    on_intel do
      url "https://github.com/sourcegit-scm/sourcegit/releases/download/v#{version}/sourcegit_#{version}.osx-x64.zip"
      sha256 "15d40c22c023d1c5e63448cafa208596a8442b636f18e7eb600d5e3abdaf635a"
    end
  end

  on_linux do
    on_arm do
      url "https://github.com/sourcegit-scm/sourcegit/releases/download/v#{version}/sourcegit-#{version}.linux.arm64.AppImage"
      sha256 "7a1dcdbb572b4b6dcfe2e8c11053e58e3af58b062b6b9966feacc6ee03eaacdb"
    end
    on_intel do
      url "https://github.com/sourcegit-scm/sourcegit/releases/download/v#{version}/sourcegit-#{version}.linux.amd64.AppImage"
      sha256 "c7aac287221e92dfd94de9863b3bc7e05a6d002790d2bf7708012515f6703c13"
    end
  end

  def install
    if OS.mac?
      prefix.install "SourceGit.app"
      bin.write_exec_script prefix/"SourceGit.app/Contents/MacOS/SourceGit"
    else
      appimage = Dir["*.AppImage"].first || "sourcegit-#{version}.linux.#{Hardware::CPU.arm? ? "arm64" : "amd64"}.AppImage"
      bin.install appimage => "sourcegit"
    end
  end

  def caveats
    if OS.linux?
      <<~EOS
        SourceGit is installed as an AppImage.
        You may need FUSE to run it: https://github.com/AppImage/AppImageKit/wiki/FUSE

        On Fedora/Bluefin: sudo dnf install fuse fuse-libs
        Or run with --appimage-extract-and-run flag.
      EOS
    end
  end

  test do
    if OS.mac?
      assert_predicate prefix/"SourceGit.app/Contents/MacOS/SourceGit", :exist?
    else
      assert_predicate bin/"sourcegit", :executable?
    end
  end
end
