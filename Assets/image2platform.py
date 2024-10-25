import argparse
import numpy as np 
import cv2
from matplotlib import pyplot as plt
from scipy import interpolate
import os
import warnings


def b_spline_to_bezier_series(tck, per=False):
    """Convert a parametric b-spline into a sequence of Bezier curves of the same degree.

    Inputs:
        tck : (t,c,k) tuple of b-spline knots, coefficients, and degree returned by splprep.
        per : if tck was created as a periodic spline, per *must* be true, else per *must* be false.

    Output:
        A list of Bezier curves of degree k that is equivalent to the input spline. 
        Each Bezier curve is an array of shape (k+1,d) where d is the dimension of the
        space; thus the curve includes the starting point, the k-1 internal control 
        points, and the endpoint, where each point is of d dimensions.
    """
    from scipy.interpolate import insert
    from numpy import asarray, unique, split, sum
    t,c,k = tck
    t = asarray(t)
    try:
        c[0][0]
    except:
        # I can't figure out a simple way to convert nonparametric splines to 
        # parametric splines. Oh well.
        raise TypeError("Only parametric b-splines are supported.")
    new_tck = tck
    if per:
        # ignore the leading and trailing k knots that exist to enforce periodicity 
        knots_to_consider = unique(t[k:-k])
    else:
        # the first and last k+1 knots are identical in the non-periodic case, so
        # no need to consider them when increasing the knot multiplicities below
        knots_to_consider = unique(t[k+1:-k-1])
    # For each unique knot, bring it's multiplicity up to the next multiple of k+1
    # This removes all continuity constraints between each of the original knots, 
    # creating a set of independent Bezier curves.
    desired_multiplicity = k+1
    for x in knots_to_consider:
        current_multiplicity = sum(t == x)
        remainder = current_multiplicity%desired_multiplicity
        if remainder != 0:
        # add enough knots to bring the current multiplicity up to the desired multiplicity
            number_to_insert = desired_multiplicity - remainder
            new_tck = insert(x, new_tck, number_to_insert, per)
    tt,cc,kk = new_tck
    # strip off the last k+1 knots, as they are redundant after knot insertion
    bezier_points = np.transpose(cc)[:-desired_multiplicity]
    if per:
        # again, ignore the leading and trailing k knots
        bezier_points = bezier_points[k:-k]
    # group the points into the desired bezier curves
    return split(bezier_points, len(bezier_points) / desired_multiplicity, axis = 0)

def densify(x, y):
    v = np.stack([x, y]).T
    v = np.concatenate([v, v[0, np.newaxis]])

    dist = np.sqrt(np.sum((v[1:, :] - v[:-1, :])**2, axis=1))
    max_dist = np.median(dist) * 0.5

    inserted = 0
    for i in range(len(dist)):
        if dist[i] > max_dist:
            n = int(np.ceil(dist[i] / max_dist))
            to_insert = np.linspace(v[i + inserted], v[i + inserted + 1], n + 1)[1:-1, :]
            v = np.concatenate([v[:i + inserted + 1, :], to_insert, v[i + inserted + 1:, :]])
            inserted += n - 1
    v = v[:-1, :]
    return v[:, 0], v[:, 1]

def collinear(a, b, c):
    # scale = np.max([a, b, c], axis=0)
    # a, b, c = a / scale, b / scale, c / scale
    r = (b - a) * np.flip(c - a)
    return abs(r[0] - r[1]) < 1e-6

def collinear_multiple(*args):
    args = list(args)
    for a, b, c in zip(args[:-2], args[1:-1], args[2:]):
        if not collinear(a, b, c):
            return False
    return True

def main(img_path, gray_threshold=110, critical_drop=0.0001, scale_fix=1, plot=0):
    if not os.path.isfile(img_path):
        raise ValueError("Img file doesn't exist. Path must be relative to main project folder")
    
    if critical_drop <= 0:
        raise ValueError("critical_drop is too small")

    num_points = 50

    img = cv2.imread(img_path, cv2.IMREAD_GRAYSCALE) 
    _, threshold = cv2.threshold(img, gray_threshold, 255, cv2.THRESH_BINARY) 

    contours, _ = cv2.findContours(threshold, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE) 

    total_area = img.shape[0] * img.shape[1]
    min_side = min(img.shape[:2])

    num_contours = 0
    for cnt in contours: 
        area = cv2.contourArea(cnt) 
        if area < total_area * 0.99:
            num_contours += 1
    
    print(num_contours)

    for cnt in contours: 
        area = cv2.contourArea(cnt) 
        if area < total_area * 0.99:
            x = cnt[:, 0, 0] / min_side
            y = (img.shape[0] - cnt[:, 0, 1]) / min_side

            if scale_fix:
                min_x, max_x = min(x), max(x)
                min_y, max_y = min(y), max(y)
            else:
                min_x, max_x = 0.0, 1.0
                min_y, max_y = 0.0, 1.0

            scale = max(max_x - min_x, max_y - min_y)
            x = (x - min_x) / scale
            y = (y - min_y) / scale
            reverse_vec = np.array([min_x, min_y])

            x, y = densify(x, y)            

            tck, u = interpolate.splprep([x, y], s=critical_drop, k=3, per=True)

            unew = np.arange(0, 1 + 1 / num_points, 1 / num_points)
            out = interpolate.splev(unew, tck)

            res = b_spline_to_bezier_series(tck, per=True)
            points_bez = np.array([el[0] for el in res])

            if plot:
                plt.plot(x, y, label='orig shape')
                plt.scatter(x, y, label='orig shape')
                plt.scatter(out[0], out[1], label='splprep')
                plt.scatter(points_bez[:, 0], points_bez[:, 1], label='bezier_before')

            points = [vec[0] * scale + reverse_vec for vec in res]
            tangent_right = [vec[1] * scale + reverse_vec for vec in res]
            tangent_left = [vec[2] * scale + reverse_vec for vec in [res[-1]] + res[:-1]]
                
            i = 0
            points_bez = points_bez.tolist()
            while i < len(points):
                left = i - 1
                right = i + 1 if i != len(points) - 1 else 0
                # if collinear_multiple(points[left], tangent_right[left], tangent_left[i], points[i], tangent_right[i], tangent_left[right], points[right]):
                if collinear_multiple(points[left], tangent_right[left], points[i], tangent_left[right], points[right]):
                    del points[i]
                    del tangent_right[i]
                    del tangent_left[i]
                    del points_bez[i]
                else:
                    i += 1
            points_bez = np.array(points_bez)

            if plot:
                plt.scatter(points_bez[:, 0], points_bez[:, 1], label='bezier_after')

            center = sum(points) / len(points)

            print(len(points))
            print(*center, sep='\t')
            for vec in zip(points, tangent_right, tangent_left):
                print(*vec[0], *vec[1], *vec[2], sep='\t')

    if plot:
        if scale_fix:
            plt.xlim(-0.2, 1.2)
            plt.ylim(-0.2, 1.2)
        else:
            plt.xlim(0, img.shape[1] / min_side)
            plt.ylim(0, img.shape[0] / min_side)
        plt.legend()
        plt.show()

if __name__ == "__main__":
    warnings.filterwarnings("ignore")

    parser = argparse.ArgumentParser()
    parser.add_argument("image-path", help="path to image with platforms", type=str)
    parser.add_argument("gray-threshold", help="level of gray to distinct black and white", type=int)
    parser.add_argument("critical-drop", help="level of accuracy (lower is better)", type=float)
    parser.add_argument("scale-fix", help="tweak scale of each platform on image or not", type=int)
    args = parser.parse_args()
    main(vars(args)['image-path'], vars(args)['gray-threshold'], vars(args)['critical-drop'], vars(args)['scale-fix'])