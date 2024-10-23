import argparse
import numpy as np 
import cv2
from matplotlib import pyplot as plt
from scipy import interpolate
import os


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


def main(img_path, gray_threshold=110, critical_drop=0.0001):
    if not os.path.isfile(img_path):
        raise ValueError("Img file doesn't exist. Path must be relative to main project folder")
    
    if critical_drop <= 0:
        raise ValueError("critical_drop is too small")

    num_points = 10000

    img = cv2.imread(img_path, cv2.IMREAD_GRAYSCALE) 
    _,threshold = cv2.threshold(img, gray_threshold, 255, cv2.THRESH_BINARY) 

    contours,_=cv2.findContours(threshold, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE) 

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

            tck, u = interpolate.splprep([x, y], s=critical_drop, k=3, per=True)
            # print(len(tck[0]))
            # print(tck)
            # print(u)
            # num_points = len(tck[0])
            unew = np.arange(0, 1 + 1 / num_points, 1 / num_points)
            out = interpolate.splev(unew, tck)
            # out = interpolate.splev(u, tck)

            # plt.scatter(x, y)
            # plt.plot(out[0], out[1])
            # plt.scatter(out[0], out[1])


            res = b_spline_to_bezier_series(tck, per=True)
            # print(res)
            # print(len(res))
            # points_bez = np.array([el[0] for el in res])
            # plt.scatter(points_bez[:, 0], points_bez[:, 1])

            print(len(res))
            points = [vec[0] for vec in res]
            center = sum(points) / len(points)
            print(*center, sep='\t')
            tangent_right = [vec[1] for vec in res]
            tangent_left = [vec[2] for vec in [res[-1]] + res[:-1]]
            for vec in zip(points, tangent_right, tangent_left):
                print(*vec[0], *vec[1], *vec[2], sep='\t')


        
    # if img.shape[0] < img.shape[1]:
    #     plt.xlim(0, img.shape[1] / img.shape[0])
    #     plt.ylim(0, 1)
    # else:
    #     plt.xlim(0, 1)
    #     plt.ylim(0, img.shape[0] / img.shape[1])

    # plt.show()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("image-path", help="path to image with platforms", type=str)
    parser.add_argument("gray-threshold", help="level of gray to distinct black and white", type=int)
    parser.add_argument("critical-drop", help="level of accuracy (lower is better)", type=float)
    args = parser.parse_args()
    main(vars(args)['image-path'], vars(args)['gray-threshold'], vars(args)['critical-drop'])