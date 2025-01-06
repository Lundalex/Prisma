import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import Rectangle

class TraverseLineSegmentDemo:
    def __init__(self):
        self.start_pos = np.array([2, 2])
        self.end_pos = np.array([10, 8])

        self.fig, self.ax = plt.subplots()
        self.ax.set_xlim(0, 15)
        self.ax.set_ylim(0, 15)
        self.ax.set_aspect('equal', adjustable='box')
        self.ax.grid(True)

        self.line, = self.ax.plot([self.start_pos[0], self.end_pos[0]], [self.start_pos[1], self.end_pos[1]], 'bo-')
        self.traversed_cells = []
        self.update_traversed_cells()

        self.cid = self.fig.canvas.mpl_connect('button_press_event', self.on_click)

    def update_traversed_cells(self):
        for rect in self.traversed_cells:
            rect.remove()
        self.traversed_cells.clear()

        for cell in self.traverse_line_segment(self.start_pos[0], self.start_pos[1], self.end_pos[0], self.end_pos[1], margin=5):
            rect = Rectangle((cell[0], cell[1]), 1, 1, color="yellow", alpha=0.3)
            self.ax.add_patch(rect)
            self.traversed_cells.append(rect)
        self.fig.canvas.draw_idle()

    def traverse_line_segment(self, startX, startY, endX, endY, margin=1):
        traversed_chunks = set()
        steep = abs(endY - startY) > abs(endX - startX)

        if steep:
            startX, startY = startY, startX
            endX, endY = endY, endX

        reversed = startX > endX
        if reversed:
            startX, endX = endX, startX
            startY, endY = endY, startY

        startX, startY, endX, endY = map(int, [startX, startY, endX, endY])

        dx = endX - startX
        dy = abs(endY - startY)
        error = int(dx / 2)
        y = startY
        ystep = 1 if startY < endY else -1

        for x in range(startX, endX + 1):
            for mx in range(-margin, margin + 1):
                for my in range(-margin, margin + 1):
                    coord = (y + my, x + mx) if steep else (x + mx, y + my)
                    traversed_chunks.add(coord)

            error -= dy
            if error < 0:
                y += ystep
                error += dx

        return list(traversed_chunks)

    def on_click(self, event):
        if event.inaxes != self.ax or event.xdata is None or event.ydata is None:
            return

        if event.button == 1:
            self.start_pos = np.array([event.xdata, event.ydata])
        elif event.button == 3:
            self.end_pos = np.array([event.xdata, event.ydata])

        self.line.set_data([self.start_pos[0], self.end_pos[0]], [self.start_pos[1], self.end_pos[1]])
        self.update_traversed_cells()

demo = TraverseLineSegmentDemo()
plt.show()
