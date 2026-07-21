import Gtk
import HoloCore

public class DeskMapController {
    public let container: Box

    private var zoneBoxes: [DeskZone: Box] = [:]
    private var zoneCountLabels: [DeskZone: Label] = [:]

    public let armButton: Button
    public let statusLabel: Label

    private let calibrationController = CalibrationController()

    public init() {
        container = Box(orientation: .vertical, spacing: 10)

        statusLabel = Label(text: "Initializing...")
        statusLabel.name = "status-label"

        armButton = Button(label: "Arm Zone")
        armButton.name = "arm-button"
        armButton.sensitive = false

        let mapContainer = Box(orientation: .horizontal, spacing: 40)
        mapContainer.name = "desk-map"

        // Left Rail
        let leftRail = Box(orientation: .vertical, spacing: 0)
        leftRail.name = "rail"
        let lr = createZone(name: "Left Rear", sub: "Left", zone: .leftTop)
        let lf = createZone(name: "Left Front", sub: "Left", zone: .leftBottom)
        leftRail.packStart(child: lr, expand: true, fill: true, padding: 0)
        leftRail.packStart(child: lf, expand: true, fill: true, padding: 0)

        // Right Rail
        let rightRail = Box(orientation: .vertical, spacing: 0)
        rightRail.name = "rail"
        let rr = createZone(name: "Right Rear", sub: "Right", zone: .rightTop)
        let rf = createZone(name: "Right Front", sub: "Right", zone: .rightBottom)
        rightRail.packStart(child: rr, expand: true, fill: true, padding: 0)
        rightRail.packStart(child: rf, expand: true, fill: true, padding: 0)

        // MacBook Silhouette
        let macbook = Box(orientation: .vertical, spacing: 10)
        macbook.name = "macbook"
        let macLabel = Label(text: "MacBook")
        macLabel.name = "mac-label"
        macbook.packStart(child: macLabel, expand: true, fill: false, padding: 0)

        let centerWrapper = Box(orientation: .vertical, spacing: 0)
        centerWrapper.packStart(child: macbook, expand: true, fill: false, padding: 0)

        mapContainer.packStart(child: leftRail, expand: true, fill: true, padding: 20)
        mapContainer.packStart(child: centerWrapper, expand: true, fill: true, padding: 0)
        mapContainer.packStart(child: rightRail, expand: true, fill: true, padding: 20)

        // Bottom controls
        let controlBox = Box(orientation: .vertical, spacing: 15)

        let buttonWrapper = Box(orientation: .horizontal, spacing: 0)
        buttonWrapper.packStart(child: armButton, expand: true, fill: false, padding: 0)

        controlBox.packStart(child: statusLabel, expand: false, fill: false, padding: 10)
        controlBox.packStart(child: buttonWrapper, expand: false, fill: false, padding: 0)

        container.packStart(child: mapContainer, expand: true, fill: true, padding: 0)
        container.packStart(child: controlBox, expand: false, fill: false, padding: 20)

        calibrationController.delegate = self
        armButton.connect(signal: .clicked) { [weak self] in
            self?.calibrationController.armCurrentZone()
        }
    }

    public func startCalibration() {
        calibrationController.start()
    }

    private func createZone(name: String, sub: String, zone: DeskZone) -> Widget {
        let box = Box(orientation: .horizontal, spacing: 10)
        box.name = "zone"
        zoneBoxes[zone] = box

        let labelBox = Box(orientation: .vertical, spacing: 2)
        let title = Label(text: name)
        title.name = "zone-title"
        title.halign = zone.isLeft ? .end : .start

        let subTitle = Label(text: sub)
        subTitle.name = "zone-sub"
        subTitle.halign = zone.isLeft ? .end : .start

        labelBox.valign = .center
        labelBox.packStart(child: title, expand: false, fill: false, padding: 0)
        labelBox.packStart(child: subTitle, expand: false, fill: false, padding: 0)

        let countLabel = Label(text: "0")
        countLabel.name = "zone-count"
        countLabel.valign = .center
        zoneCountLabels[zone] = countLabel

        if zone.isLeft {
            box.packStart(child: countLabel, expand: true, fill: false, padding: 20)
            box.packEnd(child: labelBox, expand: true, fill: false, padding: 20)
        } else {
            box.packStart(child: labelBox, expand: true, fill: false, padding: 20)
            box.packEnd(child: countLabel, expand: true, fill: false, padding: 20)
        }

        return box
    }

    public func updateState(activeZone: DeskZone?, counts: [DeskZone: Int], isArmed: Bool) {
        // Update every zone's count label and style
        for zone in DeskZone.allCases {
            let count = counts[zone] ?? 0
            zoneCountLabels[zone]?.text = "\(count)"

            if let box = zoneBoxes[zone] {
                if zone == activeZone {
                    box.name = isArmed ? "zone-armed" : "zone-active"
                } else {
                    box.name = "zone"
                }
            }
        }

        if let active = activeZone {
            if isArmed {
                statusLabel.text = "Listening for taps in \(active.displayName)..."
                armButton.sensitive = false
            } else {
                statusLabel.text = "Move your hand to \(active.displayName) and click Arm."
                armButton.sensitive = true
            }
        } else {
            statusLabel.text = "Calibration Complete! Profile Saved."
            armButton.sensitive = false
        }
    }
}

extension DeskMapController: CalibrationUIDelegate {
    public func didUpdateCalibrationState(activeZone: DeskZone?, counts: [DeskZone: Int], isArmed: Bool) {
        updateState(activeZone: activeZone, counts: counts, isArmed: isArmed)
    }

    public func calibrationDidComplete(profile: HoloProfile) {
        updateState(activeZone: nil, counts: [:], isArmed: false)
        statusLabel.text = "Calibration Complete! Profile Saved."
    }
}
