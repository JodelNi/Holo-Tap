import Gtk
import Gdk
import CGtk

// Keep the CSS provider and the DeskMapController at module scope so Swift never
// deallocates them while the GTK main loop is running.
let globalCssProvider = CssProvider()
var globalDeskMap: DeskMapController?

let status = Application.run(startupHandler: { app in
    print("HoloUI Startup")
}, activationHandler: { app in
    let window = ApplicationWindow(application: app)
    window.title = "Holo Calibration"
    window.setDefaultSize(width: 860, height: 540)

    let css = """
    window {
        background-color: #1a1a1a;
    }
    #desk-map {
        padding: 40px;
    }
    #rail {
        background-color: rgba(255, 255, 255, 0.04);
        border: 1px solid rgba(255, 255, 255, 0.1);
        border-radius: 15px;
    }
    #zone {
        border-bottom: 1px solid rgba(255, 255, 255, 0.05);
        padding: 10px;
    }
    #zone-active {
        border-bottom: 1px solid rgba(255, 255, 255, 0.05);
        background-color: rgba(255, 255, 255, 0.1);
        border-radius: 10px;
        padding: 10px;
    }
    #zone-armed {
        border-bottom: 1px solid rgba(255, 60, 60, 0.3);
        background-color: rgba(255, 60, 60, 0.15);
        border-radius: 10px;
        padding: 10px;
    }
    #zone-title {
        color: white;
        font-weight: bold;
        font-size: 14px;
    }
    #zone-sub {
        color: rgba(255, 255, 255, 0.5);
        font-size: 11px;
    }
    #zone-count {
        color: rgba(255, 255, 255, 0.85);
        font-weight: bold;
        font-size: 22px;
    }
    #macbook {
        background-color: rgba(255, 255, 255, 0.02);
        border: 1px solid rgba(255, 255, 255, 0.12);
        border-radius: 14px;
        min-width: 220px;
        min-height: 220px;
    }
    #mac-label {
        color: rgba(255, 255, 255, 0.45);
        font-size: 12px;
    }
    #status-label {
        color: rgba(255, 255, 255, 0.7);
        font-size: 13px;
    }
    #arm-button {
        background-color: rgba(80, 130, 255, 0.85);
        color: white;
        border-radius: 8px;
        padding: 8px 28px;
        font-weight: bold;
    }
    #arm-button:disabled {
        background-color: rgba(255, 255, 255, 0.1);
        color: rgba(255, 255, 255, 0.3);
    }
    """

    css.withCString { cstr in
        let _ = try? globalCssProvider.loadFrom(data: cstr, length: -1)
    }
    if let c_screen = gdk_screen_get_default() {
        gtk_style_context_add_provider_for_screen(c_screen, globalCssProvider.style_provider_ptr, 600)
    }

    let deskMap = DeskMapController()
    // Store in the global so ARC doesn't release it when this closure returns
    globalDeskMap = deskMap

    window.add(widget: deskMap.container)
    window.showAll()
    deskMap.startCalibration()
})

guard status == 0 else {
    fatalError("Application failed with exit code \(String(describing: status))")
}
