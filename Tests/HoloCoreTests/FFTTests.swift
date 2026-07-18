import XCTest
@testable import HoloCore

final class FFTTests: XCTestCase {
    func testPowerSpectrumUsesRequestedPowerOfTwoSize() {
        let spectrum = Radix2FFT.powerSpectrum([1, -1, 1], size: 6)

        XCTAssertEqual(spectrum.count, 5)
        XCTAssertTrue(spectrum.allSatisfy(\.isFinite))
        XCTAssertTrue(spectrum.allSatisfy { $0 >= 0 })
    }

    func testPowerSpectrumPeaksNearInputFrequency() throws {
        let sampleRate = 1_024.0
        let fftSize = 1_024
        let frequency = 128.0
        let samples = (0..<fftSize).map { index in
            sin(2 * Double.pi * frequency * Double(index) / sampleRate)
        }

        let spectrum = Radix2FFT.powerSpectrum(samples, size: fftSize)
        let peak = try XCTUnwrap(spectrum.enumerated().max { $0.element < $1.element })
        let binFrequency = Double(peak.offset) * sampleRate / Double(fftSize)

        XCTAssertEqual(binFrequency, frequency, accuracy: sampleRate / Double(fftSize))
    }
}
